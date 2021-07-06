using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Registration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PrideBot.Game
{
    public class ScoreReviewService
    {
        readonly ModelRepository repo;
        readonly LeaderboardImageGenerator leaderboardImageGenerator;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly LoggingService loggingService;
        readonly ShipImageGenerator shipImageGenerator;
        readonly LeaderboardService leaderboardService;

        class UserQuizData
        {
            public string userId;
            public int quizStreak;
            public int pointTotal;
        }

        public ScoreReviewService(ModelRepository repo, LeaderboardImageGenerator leaderboardImageGenerator, IConfigurationRoot config, DiscordSocketClient client, LoggingService loggingService, ShipImageGenerator shipImageGenerator, LeaderboardService leaderboardService)
        {
            this.repo = repo;
            this.leaderboardImageGenerator = leaderboardImageGenerator;
            this.config = config;
            this.client = client;
            this.loggingService = loggingService;
            this.shipImageGenerator = shipImageGenerator;
            this.leaderboardService = leaderboardService;
        }

        public async Task<EmbedBuilder> GetClosingAnnouncementEmbed(SqlConnection connection)
        {
            var registeredUsers = await repo.GetAllRegisteredUsersAsync(connection);
            //var ships = await repo.GetAllShipsAsync(connection);

            var embed = EmbedHelper.GetEventEmbed(client.CurrentUser, config, showUser: false)
                .WithAuthor("")
                .WithTitle("The red string has connected us all!")
                .WithDescription(DialogueDict.Get("CLOSING_DESC"));

            var topShips = (await repo.GetTopShipsAsync(connection, 10)).ToList();
            var underdogs = (await repo.GetTopUnderdogShipsAsync(connection, 10)).ToList();
            var solos = (await repo.GetTopSoloShipsAsync(connection, 10)).ToList();
            embed.Fields.AddRange(leaderboardService.GetEmbedFieldsForLeaderboard(topShips, "Champions of Love:"));
            embed.Fields.AddRange(leaderboardService.GetEmbedFieldsForLeaderboard(underdogs, "Our Beloved Underdogs:"));
            embed.AddField("And some bonus categories!", DialogueDict.Get("CLOSING_BONUS"));
            embed.Fields.AddRange(GetEmbedFieldsForTopUsers(registeredUsers
                .OrderByDescending(a => a.PointsEarned)
                .Take(10)
                .ToList(), "Top-Scoring Users"));
            embed.Fields.AddRange(leaderboardService.GetEmbedFieldsForLeaderboard(solos, "Our Solo Heavy-Liftees:", true));

            embed.AddField("Honorable Quiz Mentions:", DialogueDict.Get("CLOSING_QUIZ"));
            var quizDatas = new List<UserQuizData>();
            var gyn = client.GetGyn(config);
            foreach (var user in registeredUsers)
            {
                if (gyn.GetUser(ulong.Parse(user.UserId))?.IsGYNSage(config) ?? false)
                    continue;
                var logs = await repo.GetQuizLogsForUserAsync(connection, user.UserId);
                var streak = CalculateHighestQuizStreak(logs);
                var scores = await repo.GetScoresForUserAsync(connection, user.UserId);
                var total = scores
                    .Where(a => a.AchievementId.ToLower().Contains("quiz"))
                    .Sum(a => a.PointsEarned);
                quizDatas.Add(new UserQuizData
                {
                    userId = user.UserId,
                    quizStreak = streak,
                    pointTotal = total
                });
            }
            var topScoreUsers = quizDatas
                .OrderByDescending(a => a.pointTotal)
                .Take(10);
            var topStreakUsers = quizDatas
                .OrderByDescending(a => a.quizStreak)
                .ThenByDescending(a => a.pointTotal)
                .Take(10);
            embed.AddField("Most SP Earned From Quizzes:", string.Join("\n", topScoreUsers.Select(a => 
                (gyn.GetUser(ulong.Parse(a.userId))?.Mention ?? "Unknown User")
                     + $" - **{a.pointTotal}** {EmoteHelper.SPEmote}")), true);
            embed.AddField("Longest Quiz Streaks:", string.Join("\n", topStreakUsers.Select(a => 
                (gyn.GetUser(ulong.Parse(a.userId))?.Mention ?? "Unknown User")
                     + $" - **{a.quizStreak}** days")), true);

            embed.AddField("*Aaaaand That's It!*", DialogueDict.Get("CLOSING_FINAL"));
            embed.ImageUrl = "https://cdn.discordapp.com/attachments/844354667977637900/860949290469556234/unknown.png";

            return embed;
        }

        public List<EmbedFieldBuilder> GetEmbedFieldsForTopUsers(List<User> users, string name)
        {
            var gyn = client.GetGyn(config);
            var namesList = users.Select(a =>
                $"__#{users.IndexOf(a) + 1}: **{gyn.GetUser(ulong.Parse(a.UserId))?.Mention ?? "Unknown User"}**__\n**{a.PointsEarned} {EmoteHelper.SPEmote}**\n").ToList();
            var maxLength = 10;
            var lengths = new int[1];
            lengths[0] = Math.Min((int)Math.Ceiling((double)namesList.Count / 1.0), maxLength);
            //lengths[1] = Math.Min((int)Math.Ceiling((double)namesList.Count / 3.0), maxLength);
            //lengths[2] = Math.Min(namesList.Count - lengths[0] - lengths[1], maxLength);
            var fields = new List<EmbedFieldBuilder>();
            for (int i = 0; i < lengths.Length; i++)
            {
                var field = new EmbedFieldBuilder()
                    .WithName("\u200B")
                    .WithValue(string.Join("\n", namesList.Take(lengths[i])))
                    .WithIsInline(true);
                namesList = namesList.Skip(lengths[i]).ToList();
                fields.Add(field);
            }
            fields[0].Name = name;
            return fields;
        }

        public async Task<EmbedBuilder> GetUserReviewEmbed(SqlConnection connection, string userId)
        {

            var dbUser = await repo.GetUserAsync(connection, userId);
            var dbShips = await repo.GetUserShipsAsync(connection, dbUser.UserId);
            var historicalUserShips = await repo.GetUserShipsHistoricalAsync(connection, dbUser.UserId);

            var embed = EmbedHelper.GetEventEmbed(client.CurrentUser, config, showUser: false)
                .WithAuthor("")
                .WithThumbnailUrl("https://cdn.discordapp.com/attachments/853787185978277940/860008260915298334/naotoavi.png")
                .WithTitle("Your Pride Games Results")
                .WithDescription(DialogueDict.Get("USER_REVIEW_DESC"));

            foreach (var ship in dbShips.OrderBy(a => a.Tier))
            {
                embed.AddField($"Final Results for __**{ship.GetDisplayName()}**__:",
                    GetReviewStringForShip(ship, dbUser.UserId));
            }
            var otherShips = historicalUserShips
                .Where(a => a.Tier < 0)
                .OrderByDescending(a => a.PointsEarnedByUser);
            if (otherShips.Any())
            {
                embed.Fields.AddRange(EmbedHelper.GetOverflowFields(otherShips
                        .Select(a => $"__**{a.GetDisplayName()}:**__ " +
                        GetReviewStringForShip(a, dbUser.UserId)),
                    "\n", $"Other Pairings You Earned {EmoteHelper.SPEmote} for:",
                    $"Other Pairings You Earned {EmoteHelper.SPEmote} for (cont.):"));
            }

            embed.AddField($"Your {EmoteHelper.SPEmote} Totals for This Event:",
                $"From Combined Achievements: **{dbUser.PointsEarned} {EmoteHelper.SPEmote}**" +
                $"\nTotal Contributed to Pairings: **{dbUser.PointsGivenToShips} {EmoteHelper.SPEmote}**");

            var scoreCategories = (await repo.GetScoreCategoryBreakdownsForUserAsync(connection, dbUser.UserId))
                .OrderByDescending(a => a.PointsEarned);

            var categoryStrs = scoreCategories
                .Select(a => $"**{a.Family}:**\n{a.PointsEarned} {EmoteHelper.SPEmote}")
                .ToList();

            embed.AddField($"Per Category:",
                string.Join("\n",
                categoryStrs.Where(a => categoryStrs.IndexOf(a) % 3 == 0)), true);
            if (categoryStrs.Count > 0)
            {
                var strs = categoryStrs.Where(a => categoryStrs.IndexOf(a) % 3 == 1);
                if (strs.Any())
                    embed.AddField("\u200B",
                        string.Join("\n",
                        strs), true);
            }
            if (categoryStrs.Count > 1)
            {
                var strs = categoryStrs.Where(a => categoryStrs.IndexOf(a) % 3 == 2);
                if (strs.Any())
                    embed.AddField("\u200B",
                        string.Join("\n",
                        strs), true);
            }

            var quizLogs = await repo.GetQuizLogsForUserAsync(connection, dbUser.UserId);
            var attempted = quizLogs
                .Where(a => a.Attempted);
            if (attempted.Any())
            {
                var guessCounts = Enumerable.Range(0, 3)
                    .Select(a => attempted.Count(aa => aa.Correct && aa.Guesses == (a + 1)))
                    .ToList();
                var longestStreak = CalculateHighestQuizStreak(quizLogs);
                embed.AddField("Quiz Performance:",
                    $"**Total Attempted**: {attempted.Count()}" +
                    $"\n**Second Guess:** {guessCounts[1]} ({MathHelper.ToPercent((decimal)guessCounts[1] / (decimal)attempted.Count())}%)" +
                    $"\n**Unsuccessful:** {attempted.Count() - guessCounts.Sum()} " +
                        $"({MathHelper.ToPercent((decimal)(attempted.Count() - guessCounts.Sum()) / (decimal)attempted.Count())}%)", true);
                embed.AddField("\u200B",
                    $"\n**First Guess:** {guessCounts[0]} ({MathHelper.ToPercent((decimal)guessCounts[0] / (decimal)attempted.Count())}%)" +
                    $"\n**Third Guess:** {guessCounts[2]} ({MathHelper.ToPercent((decimal)guessCounts[2] / (decimal)attempted.Count())}%)" +
                    $"\n**Longest Streak:** {longestStreak} day{(longestStreak == 1 ? "" : "s")}", true);
            }

            embed.AddField("*Now, At Ease.*", DialogueDict.Get("USER_REVIEW_CLOSING"));

            var scoreStrs = Enumerable.Range(0, 3)
                .Select(a => !dbShips.Has((UserShipTier)a)
                    ? null
                    : "#" + dbShips.Get((UserShipTier)a).Place.ToString())
                .ToArray();
            var imagePath = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips);//, scoreTexts: scoreStrs);
            embed.ImageUrl = config.GetRelativeHostPathWeb(imagePath);
            return embed;
        }


        string GetReviewStringForShip(UserShip ship, string userId)
        {
            var str = $"**{ship.Place.ToString() + MathHelper.GetPlacePrefix((int)ship.Place)} Place**" +
                    $" with **{ship.PointsEarned.ToString()} {EmoteHelper.SPEmote}**" +
                    $" (**{ship.PointsEarnedByUser}** from you)";
            if (ship.UnderdogPlace > 0)
                str += $"\n+ **{ship.UnderdogPlace.ToString() + MathHelper.GetPlacePrefix((int)ship.UnderdogPlace)}** in the **underdogs board** (5 or less supporters)";
            if (ship.SoloPlace > 0 && (ship.TopSupporter ?? "").Equals(userId))
                str += $"\n+ **{ship.UnderdogPlace.ToString() + MathHelper.GetPlacePrefix((int)ship.SoloPlace)}** in the **solo board** thanks to you!";
            return str;
        }

        public int CalculateHighestQuizStreak(IEnumerable<QuizLog> quizLogs)
        {
            quizLogs = quizLogs
                .Where(a => a.Attempted)
                .OrderBy(a => a.Day);
            var highest = 0;
            var currentStreak = 0;
            foreach (var log in quizLogs)
            {
                if (log.Correct && log.Guesses == 1)
                {
                    currentStreak++;
                    if (currentStreak > highest)
                        highest = currentStreak;
                }
                else
                    currentStreak = 0;
            }
            return highest;
        }
    }
}