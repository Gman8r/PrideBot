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
using System.Diagnostics;
using Newtonsoft.Json;
using PrideBot.Repository;
using PrideBot.Registration;
using PrideBot.Models;
using PrideBot.Game;
using PrideBot.Quizzes;
using PrideBot.Events;

namespace PrideBot.Modules
{
    [Name("Admin")]
    [RequireSage]
    public class AdminModule : PrideModuleBase
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly ShipImageGenerator shipImageGenerator;
        private readonly DiscordSocketClient client;
        private readonly ScoringService scoringService;
        readonly AnnouncementService announcementService;
        private readonly SceneDialogueService sceneDialogueService;
        private readonly ScoreReviewService scoreReviewService;
        private readonly LoggingService loggingService;

        public AdminModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, ShipImageGenerator shipImageGenerator, DiscordSocketClient client, ScoringService scoringService, AnnouncementService announcementService, SceneDialogueService sceneDialogueService, ScoreReviewService scoreReviewService, LoggingService loggingService)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
            this.scoringService = scoringService;
            this.announcementService = announcementService;
            this.sceneDialogueService = sceneDialogueService;
            this.scoreReviewService = scoreReviewService;
            this.loggingService = loggingService;
        }

        [Command("giveachievement")]
        [Alias("grantachievement")]
        [Summary("Gives a user an achievement, same as adding reactions.")]
        [Priority(1)]
        [RequireGyn]
        [RequireContext(ContextType.Guild)]
        public async Task GiveAchievement(SocketUser user, string achievementId, bool ignoreCooldown = false, [DefaultValueName("achievement default")] int score = 0)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            if (achievement == null)
                throw new CommandException("Nope nuh-uh, I couldn't find that achievement like Anywheeeere, sorry! Make sure the Achievement Id matches the one in the sheet!");
            await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, Context.User, DateTime.Now, Context.Message, score, ignoreCooldown: ignoreCooldown,
                reportChannel: Context.Channel);
            //if (Context.Client.GetGyn(config).GetChannelfromConfig(config, "achievementschannel").Id != Context.Channel.Id)
            await ReplyResultAsync("Done!");
        }

        [Command("giveachievement")]
        [Alias("grantachievement")]
        [Summary("Gives multiple users an achievement, same as adding reactions.")]
        [Priority(0)]
        [RequireGyn]
        [RequireContext(ContextType.Guild)]
        public async Task GiveAchievement(string achievementId, params SocketUser[] users)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            if (achievement == null)
                throw new CommandException("Nope nuh-uh, I couldn't find that achievement like Anywheeeere, sorry! Make sure the Achievement Id matches the one in the sheet!");
            foreach (var user in users)
            {
                await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, Context.User, DateTime.Now, Context.Message, reportChannel: Context.Channel);
            }
            await ReplyResultAsync("Done!");
        }

        [Command("revokeachievement")]
        [Alias("removeachievement")]
        [Summary("Removes a given achievement via its scoreboard message.")]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveAchievement(MessageUrl achievementdMessageUrl)
        {
            var message = achievementdMessageUrl.Value;
            var footerText = message.Embeds.FirstOrDefault()?.Footer?.Text;
            if (footerText == null)
                throw new CommandException("Noooope you gotta link to a valid achievement post from the achievement board.");
            int groupId;
            var groupIdStr = footerText.Split().FirstOrDefault() ?? "INVALID";
            if (!int.TryParse(groupIdStr, out groupId))
                throw new CommandException("Noooope you gotta link to a valid achievement post from the achievement board.");

            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var result = await repo.DeleteScoreAsync(connection, groupId.ToString());
            if (result <= 0)
                throw new CommandException("HMMMM sorry bestie, I couldn't find the achievement from that message. Did it get removed already?");

            await ReplyResultAsync("Daaaamn OK then, I have reversed the waves of love (just for a bit) and revoked the achievement!");
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(0)]
        [Summary("Relays a message.")]
        public async Task Echo([Remainder] string message)
        {
            await ReplyAsync(DialogueDict.GenerateEmojiText(message));
        }

        [Command("emoji")]
        [Alias("emote")]
        [Priority(0)]
        [Summary("Relays a message with emoji.")]
        public async Task Emoji([Remainder] string message)
        {
            await ReplyAsync(DialogueDict.GenerateEmojiText(message));
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(1)]
        [Summary("Relays a message in the specified chat channel.")]
        public async Task Echo(SocketTextChannel channel, [Remainder] string message)
        {
            await channel.SendMessageAsync(DialogueDict.GenerateEmojiText(message));
            await ReplyResultAsync("Done!");
        }

        [Command("sayas")]
        [Alias("echo")]
        [Summary("Relays a message in the specified chat channel using an RP bot.")]
        public async Task Echo(SocketUser user, SocketTextChannel channel, [Remainder] string message)
        {
            await sceneDialogueService.SpeakAs(user, channel, message);
            await ReplyResultAsync("Done!");
        }

        [Command("attachment")]
        [Alias("attach", "file")]
        [Priority(0)]
        [Summary("Relays a message.")]
        public async Task File(string url, [Remainder] string message)
        {
            await File(Context.Channel as SocketTextChannel, url, message);
        }

        [Command("attachment")]
        [Alias("attach", "file")]
        [Priority(1)]
        [Summary("Relays a message in the specified chat channel.")]
        public async Task File(SocketTextChannel channel, string url, [Remainder] string message)
        {
            byte[] attachBytes;
            try
            {
                attachBytes = await WebHelper.DownloadWebFileDataAsync(url);
            }
            catch (Exception e)
            {
                throw new CommandException("UH OH i couldn't download that url!");
            }
            var attachStream = new MemoryStream(attachBytes);
            await channel.SendFileAsync(attachStream, "content" + Path.GetExtension(url), message);
            if (channel != Context.Channel as SocketTextChannel)
                await ReplyResultAsync("Done!");
        }

        [Command("userreview")]
        [Priority(0)]
        [Summary("Shows a review embed for a given user.")]
        public async Task UserReview(string userId)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var embed = await scoreReviewService.GetUserReviewEmbed(connection, userId);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("userreview")]
        [Priority(1)]
        [Summary("Shows a review embed for a given user.")]
        public async Task UserReview(IUser user = null)
        {
            user ??= Context.User;
            await UserReview(user.Id.ToString());
        }

        [Command("updaterules")]
        [Priority(1)]
        [Summary("Updates da rules")]
        public async Task UpdateRules()
        {
            await announcementService.UpdateRulesAsync(Context.Guild);
        }


        [Command("closingannouncement")]
        [Summary("Displays the closing announcement in this channel.")]
        public async Task ClosingAnnouncement(IUser user = null)
        {
            using var typing = Context.Channel.EnterTypingState();
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var embed = await scoreReviewService.GetClosingAnnouncementEmbed(connection);
            await ReplyAsync(embed: embed.Build());
        }


        [Command("testfinalemessages")]
        [Alias("testfinale")]
        [Summary("Tests all user result messages.")]
        public async Task FinaleTest()
        {
            using var typing = Context.Channel.EnterTypingState();
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUsers = await repo.GetAllRegisteredUsersAsync(connection);
            foreach (var user in dbUsers)
            {
                var embed = await scoreReviewService.GetUserReviewEmbed(connection, user.UserId);
                await ReplyAsync(embed: embed.Build());
            }
        }


        [Command("finale")]
        [Summary("Buh-bye, so long!")]
        public async Task Finale()
        {
            var typing = Context.Channel.EnterTypingState();
            Dictionary<string, EmbedBuilder> userEmbeds = null;
            try
            {
                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                var dbUsers = await repo.GetAllRegisteredUsersAsync(connection);
                userEmbeds = new Dictionary<string, EmbedBuilder>();
                foreach (var user in dbUsers)
                {
                    userEmbeds[user.UserId] = await scoreReviewService.GetUserReviewEmbed(connection, user.UserId);
                }

                var finaleEmbed = await scoreReviewService.GetClosingAnnouncementEmbed(connection);
                await ReplyAsync(Context.Client.GetGyn(config).GetRoleFromConfig(config, "registeredrole").Mention ,embed: finaleEmbed.Build());
            }
            finally
            {
                typing.Dispose();
            }

            var rpClient = sceneDialogueService.rpClients.FirstOrDefault(a => a.CurrentUser.Id.Equals(811051848093270027));
            //foreach (var userEmbed in userEmbeds) 
            //{
            //    await Context.Channel.SendMessageAsync(userEmbed.Key, embed: userEmbed.Value.Build());
            //}

            var SendMessageTasks = new List<Task>();
            foreach (var userEmbed in userEmbeds)
            {
                var user = rpClient.GetGyn(config).GetUser(ulong.Parse(userEmbed.Key));
                if (user == null)
                    continue;

                SendMessageTasks.Add(MessageUserAsync(user, userEmbed.Value));
            }
            Task.WaitAll(SendMessageTasks.ToArray());
            Environment.Exit(0);
        }

        public async Task MessageUserAsync(IUser user, EmbedBuilder embed)
        {
            
            try
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "Finale", $"DM'ing {user.Username} with results"));
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed.Build());
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Warning, "Finale", $"Sent DM to {user.Username} with results"));
            }
            catch
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Warning, "Finale", $"Failed to DM {user.Username} with results"));
            }
        }


    }
}