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
    class StarboardScoringService
    {
        const ulong CarlBotId = 235148962103951360;

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly IServiceProvider provider;
        readonly PluralKitApiService pluralKitApiService;

        public StarboardScoringService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, IServiceProvider provider, PluralKitApiService pluralKitApiService)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.provider = provider;

            client.MessageReceived += MessageReceived;
            client.ReactionAdded += ReactionCheck;
            this.pluralKitApiService = pluralKitApiService;
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
                if (!GameHelper.IsEventOccuring(config)) return;
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

                var guild = client.GetGuild((message.Channel as IGuildChannel)?.Guild.Id ?? 0);
                var user = await pluralKitApiService.GetUserOrPkUserAsync(guild, message);
                if (user == null) return;

                var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();

                var starboardPost = await repo.GetStarboardPostAsync(connection, userMessage.Id.ToString());
                if (starboardPost != null) return;
                starboardPost = new StarboardPost()
                {
                    MessageId = userMessage.Id.ToString(),
                    UserId = user.Id.ToString(),
                    StarCount = 0
                };
                await DatabaseHelper.GetInsertCommand(connection, starboardPost, "STARBOARD_POSTS").ExecuteNonQueryAsync();

                await userMessage.Channel.SendMessageAsync("Base achievement");
                await scoringService.AddAndDisplayAchievementAsync(connection, user, "STARBOARD", client.CurrentUser, userMessage.Timestamp.ToDateTime(), userMessage, titleUrl: message.GetJumpUrl());
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

        private Task ReactionCheck(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chnl, SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                if (!GameHelper.IsEventOccuring(config)) return;
                if (!reaction.Emote.ToString().Equals("⭐")) return;
                var message = await msg.GetOrDownloadAsync();
                if (!(message.Channel is IGuildChannel gChannel)) return;
                if (gChannel.Guild.Id != client.GetGyn(config).Id) return;

                using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
                var sbPost = await repo.GetStarboardPostAsync(connection, message.Id.ToString());
                if (sbPost == null) return;

                var messageStarCount = (await message.GetReactionUsersAsync(reaction.Emote, 100).FlattenAsync())
                    .Count(a => !a.IsBot);
                if (messageStarCount <= sbPost.StarCount) return;

                var scoreableAchievements = (await repo.GetAllStarboardAchievementAsync(connection))
                    .Where(a => a.StarCount >= sbPost.StarCount)
                    .ToList();

                sbPost.StarCount = messageStarCount;
                await DatabaseHelper.GetUpdateCommand(connection, sbPost, "STARBOARD_POSTS").ExecuteNonQueryAsync();

                if (!scoreableAchievements.Any()) return;

                var earnedAchievements = scoreableAchievements
                    .Where(a => messageStarCount >= a.StarCount)
                    .OrderBy(a => a.StarCount)
                    .ToList();
                if (!earnedAchievements.Any()) return;

                var guild = client.GetGuild((message.Channel as IGuildChannel)?.Guild.Id ?? 0);
                var user = await pluralKitApiService.GetUserOrPkUserAsync(guild, message);
                if (user == null) return;

                foreach (var achievement in earnedAchievements)
                {
                    await message.Channel.SendMessageAsync(achievement.AchievementId);
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement.AchievementId, client.CurrentUser, message.Timestamp.ToDateTime(), message, titleUrl: message.GetJumpUrl());
                }

            }).GetAwaiter();
            return Task.CompletedTask;
        }
    }
}
