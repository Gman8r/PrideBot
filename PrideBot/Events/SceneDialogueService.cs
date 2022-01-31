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
using PrideBot.Graphics;

namespace PrideBot.Events
{
    public class SceneDialogueService
    {
        readonly DiscordSocketClient client;
        public readonly DiscordSocketClient[] rpClients;
        readonly IConfigurationRoot config;
        readonly TokenConfig tokenConfig;
        readonly ModelRepository repo;

        private int readyClients;
        private bool AreClientsReady => readyClients < rpClients.Length;

        public SceneDialogueService(DiscordSocketClient client, IConfigurationRoot config, TokenConfig tokenConfig, ModelRepository repo)
        {
            this.client = client;
            this.config = config;
            this.tokenConfig = tokenConfig;
            this.repo = repo;

            var rpTokens = tokenConfig.GetSection("rptokens").GetChildren().Select(a => a.Value).ToArray();

            rpClients = new DiscordSocketClient[rpTokens.Length];
            for (int i = 0; i < rpTokens.Length; i++)
            {
                rpClients[i] = new DiscordSocketClient();
                StartupClient(rpClients[i], rpTokens[i]).GetAwaiter();
            }
        }

        public async Task SpeakAs(IUser user, IChannel channel, string content)
        {
            var rpClient = rpClients.FirstOrDefault(a => a.CurrentUser.Id == user.Id);
            if (rpClient == null)
                throw new CommandException("HMMM nope that's not a valid RP bot!");

            await rpClient.GetGyn(config).GetTextChannel(channel.Id).SendMessageAsync(content);
        }

        public async Task PerformCutscene(string sceneId, IMessageChannel referenceChannel)
        {
            IDisposable typingState = null;

            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dialogues = await repo.GetSceneDialogueForSceneAsync(connection, sceneId);

            foreach (var dialogue in dialogues)
            {
                var dClient = (string.IsNullOrEmpty(dialogue.ClientId) || dialogue.ClientId.Equals("0"))
                    ? client
                    : rpClients.FirstOrDefault(a => a.CurrentUser.Id.Equals(ulong.Parse(dialogue.ClientId)));
                var channel = dClient.GetGyn(config).GetTextChannel(referenceChannel.Id);
                if (dialogue.TypingTime > 0)
                {
                    typingState = channel.EnterTypingState();
                    await Task.Delay(dialogue.TypingTime);
                }

                try
                {
                    switch (dialogue.Action)
                    {
                        case ("TALK"):
                            await PostDialogueAsync(dClient, dialogue, channel);
                            break;
                        case ("AVATAR"):
                            var urlData = await WebHelper.DownloadWebFileDataAsync(dialogue.Content);
                            dClient.CurrentUser.ModifyAsync(a => a.Avatar = new Image(new MemoryStream(urlData))).GetAwaiter();
                            break;
                        case ("NAME"):
                            dClient.CurrentUser.ModifyAsync(a => a.Username = dialogue.Content).GetAwaiter();
                            break;
                    }
                }
                catch (Exception e)
                {
                    var embed = EmbedHelper.GetEventErrorEmbed(null, $"Failed action {dialogue.Action}" +
                        $" {(dialogue.Content.Length > 100 ? dialogue.Content.Substring(100) + "..." : dialogue.Content)} for {dClient.CurrentUser.Mention}" +
                        $"\n\nReason: {e.Message}", client, showUser: false);
                    var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                    await modChannel.SendMessageAsync(embed: embed.Build());
                }


                if (typingState != null)
                    typingState.Dispose();
                await Task.Delay(dialogue.ReadTime);
            }
        }

        public async Task StartupClient(DiscordSocketClient client, string token)
        {
            client.Ready += RpClientReady;
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
        }

        private Task RpClientReady()
        {
            readyClients++;
            Console.WriteLine("An RP client is ready");
            return Task.CompletedTask;
        }

        private async Task<IUserMessage> PostDialogueAsync(DiscordSocketClient client, SceneDialogue dialogue, IMessageChannel channel)
        {
            var embedData = await GenerateEmbedDataAsync(client, dialogue);
            if (embedData.Item2 == null)
                return await channel.SendMessageAsync(dialogue.MessageText ?? "", embed: embedData.Item1.Build());
            else
                return await channel.SendFileAsync(embedData.Item2.Stream, embedData.Item2.FileName, dialogue.MessageText ?? "", embed: embedData.Item1.Build());
        }

        private async Task<(EmbedBuilder, MemoryFile)> GenerateEmbedDataAsync(DiscordSocketClient client, SceneDialogue dialogue)
        {
            MemoryFile file = null;
            var embed = EmbedHelper.GetEventEmbed(null, config)
                .WithTitle(StringHelper.WhitespaceCoalesce(dialogue.Title))
                .WithThumbnailUrl(StringHelper.WhitespaceCoalesce(dialogue.ThumbnailImage, client.CurrentUser.GetAvatarUrl(size: 128)))
                .WithImageUrl(StringHelper.WhitespaceCoalesce(dialogue.Attachment));

            //dialogue loop
            var lines = dialogue.Content.Split('\n', StringSplitOptions.None);
            EmbedFieldBuilder currentField = null;
            foreach (var line in lines)
            {
                if (line.ToUpper().StartsWith("FIELD:"))
                {
                    if (currentField != null)
                        embed.AddField(currentField);
                    currentField = new EmbedFieldBuilder().WithName(line.Substring(6).Trim());
                }
                else if (currentField != null)
                    currentField.Value = (currentField.Value?.ToString() ?? "") + "\n" + line;
                else
                    embed.Description = (embed.Description ?? "") + "\n" + line;
            }
            if (currentField != null)
                embed.AddField(currentField);

            if (!string.IsNullOrWhiteSpace(dialogue.YellowText))
            {
                var imageService = new YellowTextGenerator(config);
                file = await imageService.WriteYellowTextAsync(embed.ThumbnailUrl, dialogue.YellowText);
                embed.WithAttachedThumbnailUrl(file);
                //embed.ThumbnailUrl = StringHelper.WhitespaceCoalesce(dialogue.ThumbnailImage);
            }
            return (embed, file);
        }
    }
}