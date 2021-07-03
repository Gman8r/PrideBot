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

        public async Task PerformCutscene(string sceneId)
        {
            IDisposable typingState = null;

            var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dialogues = await repo.GetSceneDialogueForSceneAsync(connection, sceneId);

            foreach (var dialogue in dialogues)
            {
                var dClient = (string.IsNullOrEmpty(dialogue.ClientId) || dialogue.ClientId.Equals("0"))
                    ? client
                    : rpClients.FirstOrDefault(a => a.CurrentUser.Id.Equals(ulong.Parse(dialogue.ClientId)));
                var announcementsChannel = GetAnnouncementChannelForClient(dClient);
                if (dialogue.TypingTime > 0)
                {
                    typingState = announcementsChannel.EnterTypingState();
                    await Task.Delay(dialogue.TypingTime);
                }

                try
                {
                    switch (dialogue.Action)
                    {
                        case ("TALK"):
                            if (string.IsNullOrEmpty(dialogue.Attachment))
                                await announcementsChannel.SendMessageAsync(dialogue.Content);
                            else
                            {
                                var fileBytes = await WebHelper.DownloadWebFileDataAsync(dialogue.Attachment);
                                await announcementsChannel.SendFileAsync(new MemoryStream(fileBytes), "content." + Path.GetExtension(dialogue.Attachment), dialogue.Content);
                            }
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
                catch
                {
                    var embed = EmbedHelper.GetEventErrorEmbed(null, $"Failed action {dialogue.Action} {dialogue.Content} for {dClient.CurrentUser.Mention}", client, showUser: false);
                    var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                    await modChannel.SendMessageAsync(embed: embed.Build());
                }


                if (typingState != null)
                    typingState.Dispose();
                await Task.Delay(dialogue.ReadTime);
            }
        }

        SocketTextChannel GetAnnouncementChannelForClient(DiscordSocketClient client)
            => client.GetGyn(config).GetChannelFromConfig(config, "announcementschannel") as SocketTextChannel;

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
    }
}