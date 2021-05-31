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

namespace PrideBot.Game
{
    public class ChatScoringService
    {
        int MinChatSessionMessages => int.Parse(config["chatsessionmessages"]);

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly UserRegisteredCache userReg;

        public Dictionary<ulong, UserChatData> ChatData { get; }
        public class UserChatData
        {
            public ulong Id;
            public int messageCount;
            public DateTime expires;
        }


        public ChatScoringService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, UserRegisteredCache userReg)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.userReg = userReg;

            ChatData = new Dictionary<ulong, UserChatData>();
            client.MessageReceived += MessageReceived;
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
                if (!GameHelper.IsEventOccuring(config)) return;
                if (!(msg is SocketUserMessage message)) return;
                if (message.Author.IsBot) return;
                if (!(msg.Channel is IGuildChannel gChannel)) return;
                if (gChannel.Guild.Id != client.GetGyn(config).Id) return;
                var user = message.Author;

                if (!await userReg.GetOrDownloadAsync(user.Id.ToString()))
                    return;


                if (!ChatData.ContainsKey(user.Id) || DateTime.Now > ChatData[user.Id].expires)
                {
                    var connection = repo.GetDatabaseConnection();
                    await connection.OpenAsync();
                    var achievement = await repo.GetAchievementAsync(connection, "CHAT");
                    // Check last score for potential cooldown
                    var lastScore = await repo.GetLastScoreFromUserAndAchievementAsync(connection, user.Id.ToString(), "CHAT");
                    // If user hasn't scored within the cooldown time, just null the score
                    if (lastScore != null && DateTime.Now > lastScore.TimeStamp.AddHours(achievement.CooldownHours))
                        lastScore = null;
                    ChatData[user.Id] = new UserChatData()
                    {
                        Id = user.Id,
                        expires = (lastScore?.TimeStamp ?? DateTime.Now).AddHours(achievement.CooldownHours)
                    };
                }
                
                ChatData[user.Id].messageCount++;
                if (ChatData[user.Id].messageCount == MinChatSessionMessages)
                {
                    var connection = repo.GetDatabaseConnection();
                    await connection.OpenAsync();
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, "CHAT", client.CurrentUser);
                }

            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false)
                    .WithTitle($"Exception in {this.GetType().Name} Module");
                var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                await modChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
        }
    }
}
