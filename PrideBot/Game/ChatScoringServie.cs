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

namespace PrideBot.Quizzes
{
    class ChatScoringService
    {
        int GetChatSession(DateTime dt) => (dt.Day * 24) + (dt.Hour / int.Parse(config["chatsessionhours"]));
        int GetChatSession() => GetChatSession(DateTime.Now);
        int minChatSessionMessages => int.Parse(config["chatsessionmessages"]);

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly UserRegisteredCache userReg;

        Dictionary<string, int> userMessageCounts;
        GuildSettings guildSettings;
        int currentChatSession;

        public ChatScoringService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, UserRegisteredCache userReg)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.userReg = userReg;

            userMessageCounts = new Dictionary<string, int>();
            currentChatSession = GetChatSession();
            client.MessageReceived += MessageReceived;
            client.Ready += ClientReady;
        }

        private Task ClientReady()
        {
            DoPrepAsync().GetAwaiter();
            return Task.CompletedTask;
        }

        private async Task DoPrepAsync()
        {
            var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();
            guildSettings = await repo.GetOrCreateGuildSettingsAsync(connection, config["ids:gyn"]);
        }


        private Task MessageReceived(SocketMessage msg)
        {
            ChatCheck(msg).GetAwaiter();
            return Task.CompletedTask;
        }

        async Task ChatCheck(SocketMessage msg)
        {
            SocketTextChannel starboardChannel = null;
            try
            {
                if (!GameHelper.EventOccuring(config)) return;
                if (guildSettings == null) return;
                if (!(msg is SocketUserMessage message)) return;
                if (message.Author.IsBot) return;
                if (!(msg.Channel is IGuildChannel gChannel)) return;
                if (gChannel.Guild.Id != client.GetGyn(config).Id) return;
                var user = message.Author;

                if (!await userReg.GetOrDownloadAsync(user.Id.ToString()))
                    return;

                if (GetChatSession() != currentChatSession)
                {
                    currentChatSession = GetChatSession();
                    userMessageCounts.Clear();
                }


                if (!userMessageCounts.ContainsKey(user.Id.ToString()))
                {
                    var connection = DatabaseHelper.GetDatabaseConnection();
                    await connection.OpenAsync();
                    var lastScore = await repo.GetLastScoreFromUserAndAchievementAsync(connection, user.Id.ToString(), "CHAT");
                    // If the user already got this score during this chat session, the bot probably rebooted and shouldn't give them another achievement yet
                    if (lastScore != null && GetChatSession(lastScore.TimeStamp) == GetChatSession())
                        userMessageCounts[user.Id.ToString()] = minChatSessionMessages + 1;
                    else
                        userMessageCounts[user.Id.ToString()] = 1;
                }
                else
                    userMessageCounts[user.Id.ToString()]++;

                if (userMessageCounts[user.Id.ToString()] == minChatSessionMessages)
                {
                    var connection = DatabaseHelper.GetDatabaseConnection();
                    await connection.OpenAsync();
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, "CHAT", client.CurrentUser);
                }

            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false);
                var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                await modChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
        }
    }
}
