using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;

using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Quizzes;
using PrideBot.Game;
using PrideBot.Registration;
using PrideBot.Events;
using PrideBot.Graphics;

namespace PrideBot.Events
{
    public class RpControlMenuService
    {
        public enum Action
        {
            Content,
            User,
            Channel,
            YellowText,
            Attachment
        }

        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly DiscordSocketClient client;
        readonly SceneDialogueService sceneDialogueService;
        readonly IServiceProvider services;

        private IEnumerable<RpControl> rpControlCache;

        public RpControlMenuService(IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, SceneDialogueService sceneDialogueService, IServiceProvider services)
        {
            this.config = config;
            this.repo = repo;
            this.client = client;
            this.sceneDialogueService = sceneDialogueService;

            client.Ready += ClientReady;
            client.MessageReceived += MessageRecieved;
            this.services = services;
        }

        private Task MessageRecieved(SocketMessage msg)
        {
            Task.Run(async () =>
            {
                try
                {
                    var message = msg as SocketUserMessage;     // Ensure the message is from a user/bot
                    if (message == null) return;
                    if (message.Author.IsBot) return;     // Ignore bots

                    var context = new SocketCommandContext(client, message);     // Create the command context

                    var cachedDbPost = rpControlCache
                        .FirstOrDefault(a => a.ChannelId.Equals(msg.Channel.Id.ToString()));
                    if (cachedDbPost == null)
                        return;

                    var controlMessage = (await message.Channel.GetMessageAsync(ulong.Parse(cachedDbPost.MessageId))) as IUserMessage;

                    using var connection = await repo.GetAndOpenAltDatabaseConnectionAsync();
                    var dict = ParseEmbedDataString(controlMessage.Embeds.FirstOrDefault().Description);
                    var action = (Action)Enum.Parse(typeof(Action), dict["enter"]);
                    switch (action)
                    {
                        case Action.Content:
                            var talkClient = new DiscordSocketClient[] { client }
                                .Concat(sceneDialogueService.rpClients)
                                .FirstOrDefault(a => dict["user"].Contains(a.CurrentUser.Id.ToString()));
                            var dialogue = new SceneDialogue()
                            {
                                Action = "TALK",
                                ClientId = talkClient == client ? "0" : talkClient.CurrentUser.Id.ToString(),
                                Content = message.Content,
                                ThumbnailImage = client.CurrentUser.GetAvatarUrlOrDefault(),
                                YellowText = dict["yellowtext"],
                                Attachment = dict["attachment"]
                            };

                            var data = await sceneDialogueService.GenerateEmbedDataAsync(talkClient, dialogue);
                            var talkChannel = talkClient.GetGuild((message.Channel as SocketGuildChannel).Guild.Id)
                                .GetTextChannel(ExtractId(dict["channel"]));
                            if (data.Item2 == null)
                                await talkChannel.SendMessageAsync(embed: data.Item1.Build());
                            else
                                await talkChannel.SendFileAsync(data.Item2.Stream, data.Item2.FileName, null, embed: data.Item1.Build());
                            dict["yellowtext"] = "";
                            dict["attachment"] = "";
                            await ModifyPostRpMenuAsync(connection, controlMessage, dict);
                            break;
                        case Action.YellowText:
                            dict["yellowtext"] = message.Content;
                            dict["enter"] = Action.Content.ToString();
                            await ModifyPostRpMenuAsync(connection, controlMessage, dict);
                            break;
                        case Action.Attachment:
                            try
                            {
                                dict["attachment"] = message.Content;
                                dict["enter"] = Action.Content.ToString();
                                await ModifyPostRpMenuAsync(connection, controlMessage, dict);
                            }
                            catch (Exception e)
                            {
                                DeleteDelayed(await message.Channel.SendMessageAsync("Not a valid attachment. ❌")).GetAwaiter();
                                dict["attachment"] = "";
                                await ModifyPostRpMenuAsync(connection, controlMessage, dict);
                            }
                            break;
                        case Action.Channel:
                            try
                            {
                                var typeReader = new ChannelTypeReader<ITextChannel>();
                                var result = await typeReader.ReadAsync(context, message.Content, services);
                                dict["channel"] = (result.BestMatch as ITextChannel).Mention;
                                dict["enter"] = Action.Content.ToString();
                            }
                            catch (Exception e)
                            {
                                DeleteDelayed(await message.Channel.SendMessageAsync("Couldn't find that channel. ❌")).GetAwaiter();
                                dict["enter"] = Action.Content.ToString();
                            }
                            await ModifyPostRpMenuAsync(connection, controlMessage, dict);
                            break;
                    }

                    DeleteDelayed(message);
                }
                catch (Exception e)
                {

                }
            }).GetAwaiter();
            return Task.CompletedTask;
        }

        private async Task DeleteDelayed(IMessage message, int ms = 5000)
        {
            await Task.Delay(ms);
            await message.DeleteAsync();
        }

        private Task ClientReady()
        {
            Task.Run(async () =>
            {
                using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
                await UpdateCacheAsync(connection);
            }).GetAwaiter();
            return Task.CompletedTask;
        }

        public async Task<IUserMessage> PostRpMenuAsync(SqlConnection connection, ITextChannel channel)
        {
            // create value dict
            var dict = new Dictionary<string, string>();
            dict["user"] = client.CurrentUser.Mention;
            dict["channel"] = channel.Mention;
            dict["yellowtext"] = "";
            dict["attachment"] = "";
            dict["enter"] = Action.Content.ToString();

            var data = await GetPostDataAsync(connection, dict);
            IUserMessage message;
            if (data.Item2 != null)
                message = await channel.SendFileAsync(data.Item2.Stream, data.Item2.FileName, null, embed: data.Item1.Build(), components: data.Item3.Build());
            else
                message = await channel.SendMessageAsync(embed: data.Item1.Build(), components: data.Item3.Build());

            await repo.DeleteRpControlsInChannelAsync(connection, channel.Id.ToString());
            var dbPost = new RpControl()
            {
                ChannelId = channel.Id.ToString(),
                MessageId = message.Id.ToString()
            };
            await DatabaseHelper.GetInsertCommand(connection, dbPost, "RP_CONTROLS").ExecuteNonQueryAsync();
            await UpdateCacheAsync(connection);

            return message;
        }
        
        public async Task ModifyPostRpMenuAsync(SqlConnection connection, IUserMessage message, Dictionary<string, string> dict)
        {
            var data = await GetPostDataAsync(connection, dict);
            await message.ModifyAsync(a =>
            {
                a.Embed = data.Item1.Build();
                a.Attachments = data.Item2 != null
                    ? new List<FileAttachment>() { new FileAttachment(data.Item2.Stream, data.Item2.FileName) }
                    : new List<FileAttachment>();
                a.Components = data.Item3.Build();
            });
        }

        public async Task<(EmbedBuilder, MemoryFile, ComponentBuilder)> GetPostDataAsync(SqlConnection connection, Dictionary<string, string> dict)
        {
            var botUser = client.GetGyn(config).GetUser(ExtractId(dict["user"]));
            var yellowText = dict["yellowtext"];
            var attachment = dict["attachment"];
            var action = (Action)Enum.Parse(typeof(Action), dict["enter"]);

            // create embed
            var embed = new EmbedBuilder()
                .WithColor(botUser.Roles
                    .OrderByDescending(a => a.Position)
                    .FirstOrDefault(a => a.Color != default)?
                    .Color ?? default)
                .WithTitle("RP Control")
                .WithDescription(CreateEmbedDataString(dict))
                .WithImageUrl(!string.IsNullOrWhiteSpace(attachment) ? attachment : null)
                .WithThumbnailUrl(botUser.GetAvatarUrlOrDefault());

            // create any necessary files

            // yellow text
            MemoryFile urlFile = null;
            if (!string.IsNullOrWhiteSpace(yellowText))
            {
                urlFile = await new YellowTextGenerator(config).WriteYellowTextAsync(botUser.GetAvatarUrl(size: 128), yellowText);
                embed.WithAttachedThumbnailUrl(urlFile);
            }

            // create components
            var userOptions = new DiscordSocketClient[] { client }
                .Concat(sceneDialogueService.rpClients)
                .Select(a => a.CurrentUser)
                .Select(a => new SelectMenuOptionBuilder()
                    .WithLabel(a.Username)
                    .WithValue(a.Id.ToString())
                    .WithDefault(botUser.Id == a.Id))
                .ToList();
            var cBuilder = new ComponentBuilder()
                .WithSelectMenu("RPMENU.U", userOptions, "Select a User")
                .WithButton("Set Channel", "RPMENU.C", emote: new Emoji("📝"))
                .WithButton("Set Attachment Image", "RPMENU.A", emote: new Emoji("📎"))
                .WithButton("Set Yellow Text", "RPMENU.Y", emote: Emote.Parse("<:cake:939534146798223420>"))
                .WithButton("Reset Yellow Text", "RPMENU.RY", ButtonStyle.Secondary, disabled: string.IsNullOrWhiteSpace(yellowText))
                .WithAllDisabled(action != Action.Content);

            // headin back home
            return (embed, urlFile, cBuilder);
        }

        async Task UpdateCacheAsync(SqlConnection connection) => rpControlCache = await repo.GetAlRpControlsAsync(connection);

        public Dictionary<string, string> ParseEmbedDataString(string text)
        {
            var dict = new Dictionary<string, string>();
            foreach (var line in text.Split('\n'))
            {
                var key = line.Substring(0, line.IndexOf(':'));
                var value = line.Substring(line.IndexOf(':') + 2);
                dict[key] = value;

            }
            return dict;
        }

        string CreateEmbedDataString(Dictionary<string, string> dict)
        {
            var str = "";
            foreach (var kvp in dict)
            {
                str += $"\n{kvp.Key}: {kvp.Value}";
            }
            return str.Trim();
        }

        ulong ExtractId(string mention) => ulong.Parse(new string(mention.Where(a => char.IsDigit(a)).ToArray()));

    }
}
