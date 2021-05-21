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


namespace PrideBot.Quizzes
{
    class StarboardService
    {
        const ulong CarlBotId = 235148962103951360;

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;

        public StarboardService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;

            client.MessageReceived += MessageReceived;
        }

        private Task MessageReceived(SocketMessage msg)
        {
            StarboardCheck(msg).GetAwaiter();
            return Task.CompletedTask;
        }

        async Task StarboardCheck(SocketMessage msg)
        {
            SocketTextChannel starboardChannel = null;
            try
            {
                if (msg.Author.Id != CarlBotId) return;
                starboardChannel = client.GetGyn(config).GetChannelFromConfig(config, "starboardchannel") as SocketTextChannel;
                if (msg.Channel.Id != starboardChannel.Id) return;
                if (!(msg is SocketUserMessage message)) return;
                if (!(msg.Channel is IGuildChannel gChannel)) return;
                if (gChannel.Guild.Id != client.GetGyn(config).Id) return;
                if (message.MentionedChannels.Count() != 1) return;
                if (message.Embeds.Count < 1 || !message.Embeds.FirstOrDefault().Footer.HasValue) return;

                var userChannel = message.MentionedChannels.FirstOrDefault();
                var userMessageIdStr = message.Embeds.FirstOrDefault().Footer.Value.Text;
                ulong userMessageId;
                if (!ulong.TryParse(userMessageIdStr, out userMessageId)) return;
                var userMessage = await (userChannel as SocketTextChannel).GetMessageAsync(userMessageId);
                if (userMessage == null) return;
                if (userMessage.Author.IsBot) return;

                var connection = DatabaseHelper.GetDatabaseConnection();
                await connection.OpenAsync();
                await scoringService.AddAndDisplayAchievementAsync(connection, userMessage.Author, "STARBOARD", client.CurrentUser, titleUrl: message.GetJumpUrl());
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false);
                await starboardChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
        }
    }
}
