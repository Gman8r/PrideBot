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
using PrideBot.GDrive;

namespace PrideBot.Modules
{
    [Name("Game")]
    public class GameModule : PrideModuleBase
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly ShipImageGenerator shipImageGenerator;
        private readonly DiscordSocketClient client;
        private readonly ScoringService scoringService;
        private readonly UserRegisteredCache userReg;
        private readonly GoogleSheetsService sheetsService;


        public GameModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, ShipImageGenerator shipImageGenerator, DiscordSocketClient client, ScoringService scoringService, UserRegisteredCache userReg, GoogleSheetsService sheetsService)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
            this.scoringService = scoringService;
            this.userReg = userReg;
            this.sheetsService = sheetsService;
        }

        [Command("ships")]
        [Alias("pairings")]
        [Summary("Views your chosen pairings, or someone else's if you specify a user!")]
        public async Task Ships(SocketUser user = null)
        {
            user ??= Context.User;
            if (user.Id == Context.Client.CurrentUser.Id)
                throw new CommandException($"HEYY, omg, like that's not something you can just pry from me!");
            if (user.IsBot)
                throw new CommandException($"That's a bot...");
            var isSelf = user == Context.User;
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetUserAsync(connection, user.Id.ToString());
            var username = (user as SocketGuildUser)?.Nickname ?? user.Username;

            if (!(dbUser?.ShipsSelected ?? false))
            {
                if (isSelf)
                    throw new CommandException($"You haven't configured your pairings yet! Register with `{config.GetDefaultPrefix()}register` first.");
                else
                {
                    var pronoun = user.Pronoun(Context.Client, Pronoun.Their);
                    throw new CommandException($"{username} hasn't registered and configured {pronoun} pairings yet! 🕸");
                }
            }

            var dbShips = await repo.GetUserShipsAsync(connection, dbUser);

            var imageFile = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips);
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithAttachedImageUrl(imageFile)
                .WithThumbnailUrl(user.GetServerAvatarUrlOrDefault())
                .WithTitle($"User Overview")
                .WithDescription(isSelf ? "Here's who you're supporting! 💐" : $"Here's who {user.Mention} is supporting! 💐")
                .WithFooter(new EmbedFooterBuilder()
                    .WithText(user.Id.ToString()));

            embed.AddField("Ships Supported:",
                string.Join("\n", Enumerable.Range(0, 3)
                .Select(a => (UserShipTier)a)
                .Where(a => dbShips.Has(a))
                .Select(a => $"{EmoteHelper.GetShipTierEmoji(a)} **{a}** Pairing: **{dbShips.Get(a)?.GetDisplayName() ?? "None"}**" +
                    $" {((dbShips.Get(a).ScoreRatio == 1m || !dbShips.Has(a)) ? "" : $" ({GameHelper.GetPointPercent(dbShips.Get(a).ScoreRatio)}% SP)")}")));

            await Context.Channel.SendFileAsync(imageFile.Stream, imageFile.FileName, embed: embed.Build());
        }


        //[Command("users")]
        //public async Task Users()
        //{
        //    using var typing = Context.Channel.EnterTypingState();
        //    using var connection = repo.GetDatabaseConnection();
        //    await connection.OpenAsync();
        //    var allShips = (await repo.GetAllShipsAsync(connection))
        //        .Where(a => a.PointsEarned > 0);
        //    var ships = allShips;
        //    //.OrderByDescending(a => a.PointsEarned)
        //    //.Take(20);
        //    var scores = await repo.GetAllShipScoresAsync(connection);
        //    var achievements = await repo.GetAllAchievementsAsync(connection);

        //    var userIds = scores
        //        .GroupBy(a => a.UserId)
        //        .Select(a => ulong.Parse(a.Key))
        //        .OrderBy(a => a);
        //    //var users = userIds
        //    //    .Select(a => Context.Client.GetGyn(config).GetUser(a));


        //    var chid = 842885896756002816;
        //    var ch = await Context.Client.Rest.GetDMChannelAsync(842885896756002816);
        //    var sfskf = await Context.Client.GetDMChannelAsync(842885896756002816);

        //    var data = new List<IList<object>>();
        //    var header = new List<object>() { "" };
        //    data.Add(header);
        //    var gyn = Context.Client.GetGyn(config);
        //    var dmChannels = await Context.Client.GetDMChannelsAsync();
        //    var dm2 = await Context.Client.Rest.GetDMChannelsAsync();
        //    foreach (var id in userIds)
        //    {
        //        var user = gyn.GetUser(id);
        //        var name = user?.Nickname ?? user?.Username ?? "Unknown User";
        //        name += $" ({id})";
        //        header.Add(name);
        //        var dmChannel = dm2
        //            .FirstOrDefault(a => a.Users.Select(a => a.Id).Contains(id));
        //        if (dmChannel == null)
        //        {

        //        }
        //    }

        //    foreach (var ship in ships)
        //    {
        //        var row = new List<object>() { ship.GetDisplayName() };
        //        var shipScores = scores.Where(a => a.ShipId.Equals(ship.ShipId));
        //        foreach (var uid in userIds)
        //        {
        //            var sum = shipScores
        //                .Where(a => a.UserId.Equals(uid.ToString()))
        //                .Sum(a => a.PointsEarned);
        //            row.Add(sum);
        //        }
        //        data.Add(row);
        //    }

        //    await sheetsService.UpdateDataAsync("1bH24HKQ8Y6qWKbGlDW0YACCVuLreJeN6INCwPnsTQgs", $"A1:YY1001", data as IList<IList<object>>);
        //    await ReplyAsync("FUcking DID IT!");
        //}

        // TODO this is the old simulation code
        //[Command("review")]
        //public async Task Review()
        //{
        //    using var typing = Context.Channel.EnterTypingState();
        //    using var connection = repo.GetDatabaseConnection();
        //    await connection.OpenAsync();
        //    var allShips = (await repo.GetAllShipsAsync(connection))
        //        .Where(a => a.PointsEarned > 0);
        //    var ships = allShips;
        //        //.OrderByDescending(a => a.PointsEarned)
        //        //.Take(20);
        //    var scores = await repo.GetAllShipScoresAsync(connection);
        //    var achievements = await repo.GetAllAchievementsAsync(connection);


        //    //// Randomize ship users
        //    //var seed = 1;
        //    //var rand = new Random(seed);
        //    //var userDict = new Dictionary<string, List<Ship>>();
        //    //var userIds = scores.Select(a => a.UserId);
        //    //var validShips = ships.Where(a => a.Supporters > 0).ToList();
        //    //foreach (var userId in userIds)
        //    //{
        //    //    var shipList = new List<Ship>();
        //    //    var totalShipWeight = ships.Sum(a => a.Supporters);
        //    //    for (int i = 0; i < 3; i++)
        //    //    {
        //    //        var chosenWeight = rand.Next(totalShipWeight);
        //    //        var chosenShip = ships.FirstOrDefault();
        //    //        for (int j = 0; j < validShips.Count; j++)
        //    //        {
        //    //            chosenWeight -= validShips[j].Supporters;
        //    //            if (chosenWeight < 0)
        //    //            {
        //    //                chosenShip = validShips[j];
        //    //                break;
        //    //            }
        //    //        }
        //    //        shipList.Add(chosenShip);
        //    //    }
        //    //    userDict[userId] = shipList;
        //    //}
        //    //foreach (var ship in ships)
        //    //{
        //    //    var supporters = (double)userDict
        //    //        .Select(a => a.Value[0])
        //    //        .Count(a => a.ShipId.Equals(ship.ShipId));
        //    //    supporters += (double)userDict
        //    //        .Select(a => a.Value[1])
        //    //        .Count(a => a.ShipId.Equals(ship.ShipId)) * .4;
        //    //    supporters += (double)userDict
        //    //        .Select(a => a.Value[2])
        //    //        .Count(a => a.ShipId.Equals(ship.ShipId)) * .2;
        //    //    ship.RandomSupporters = supporters;
        //    //}
        //    //foreach (var score in scores)
        //    //{
        //    //    score.ShipId = userDict[score.UserId][score.Tier].ShipId;
        //    //}


        //    //// Flatten single achievements
        //    //var singleAchievementIds = new List<string>() { "CREATE_ART", "CREATE_MUSIC", "CREATE_FIC", "CREATE_COSPLAY", "CREATE_CRAFT", "CREATE_PRIDE", "CREATE_PAIRS", "SNAKE", "COMMISSION" };
        //    //var singleAchievementFamilies = new List<string>() { "Touhou 1CC's" };
        //    //var userScoreGroups = scores.GroupBy(a => a.UserId);
        //    //foreach (var userScores in userScoreGroups)
        //    //{
        //    //    foreach (var achievementId in singleAchievementIds)
        //    //    {
        //    //        var achievementScores = userScores.Where(a => a.AchievementId.Equals(achievementId));
        //    //        var achievementScoreIds = achievementScores.Select(a => a.ScoreId).Distinct().Skip(2); // Number allowed
        //    //        if (achievementScoreIds.Any())
        //    //        {
        //    //            achievementScores = achievementScores.Where(a => achievementScoreIds.Contains(a.ScoreId));
        //    //            scores = scores.Except(achievementScores);
        //    //        }
        //    //    }
        //    //    foreach (var achievementFamily in singleAchievementFamilies)
        //    //    {
        //    //        var familyScores = userScores
        //    //            .Where(a => achievements.FirstOrDefault(aa => aa.AchievementId.Equals(a.AchievementId)).Family?.Equals(achievementFamily) ?? false);
        //    //        var familyScoreIds = familyScores.Select(a => a.ScoreId).Distinct().Skip(2); // Number allowed
        //    //        if (familyScoreIds.Any())
        //    //        {
        //    //            familyScores = familyScores.Where(a => familyScoreIds.Contains(a.ScoreId));
        //    //            scores = scores.Except(familyScores);
        //    //        }
        //    //    }
        //    //}

        //    //// Apply creative mult
        //    //foreach (var score in scores.Where(a => singleAchievementIds.Contains(a.AchievementId)
        //    //    || singleAchievementFamilies.Contains( achievements.FirstOrDefault(aa => aa.AchievementId.Equals(a.AchievementId) ).Family ?? "")))
        //    //{
        //    //    score.PointsEarned *= 2;
        //    //}

        //    //// idk remove all of them
        //    //var singleAchievementIds = new List<string>() { "CREATE_ART", "CREATE_MUSIC", "CREATE_FIC", "CREATE_COSPLAY", "CREATE_CRAFT", "CREATE_PRIDE", "CREATE_PAIRS", "SNAKE", "COMMISSION", "1CC_EASY", "1CC_NORML", "1CC_HARD", "1CC_LUNATIC", "1CC_EXTRA" };
        //    //scores = scores.Where(a => !singleAchievementIds.Contains(a.AchievementId)).ToArray();


        //    var scoreDict = new Dictionary<Ship, decimal>();
        //    foreach (var ship in ships)
        //    {
        //        scoreDict[ship] = 0;
        //    }

        //    // Google stuff
        //    var sheetId = "1he7cmt27xJQsyhiFOZ8ODGuk6UedBfahEsikys2MfTk";
        //    var sheetValues = new List<IList<object>>();

        //    var firstRow = new List<object>();
        //    firstRow.Add("Hour");
        //    firstRow.AddRange(ships.Select(a => a.GetDisplayName()));
        //    sheetValues.Add(firstRow);
        //    //File.WriteAllText("shipscores.xls", "hour, " + string.Join(", ", ships.Select(a => a.GetDisplayName())));

        //    scores = scores.ToArray();
        //    // Prereg scores
        //    foreach (var score in scores.Where(a => a.Timestamp.Month < 6))
        //    {
        //        var ship = ships.FirstOrDefault(a => a.ShipId.Equals(score.ShipId));
        //        ship.TestPlace = (int)ship.Place;
        //        if (ship != null)
        //            scoreDict[ship] += score.PointsEarned;
        //    }
        //    scores = scores.SkipWhile(a => a.Timestamp.Month < 6);
        //    scores = scores.ToArray();
        //    var allScores = scores.ToArray();

        //    var step = 6;
        //    var maxSupporters = 0.0;
        //    for (int i = 0; i <= 24 * 30; i += step)
        //    {
        //        var hour = i % 24;
        //        var day = (i - (i % 24)) / 24;
        //        var beginTimestamp = new DateTime(2021, 6, 1).AddHours(i);
        //        var endTimestamp = beginTimestamp.AddHours(step);
        //        var hourScores = scores.TakeWhile(a => a.Timestamp < endTimestamp);
        //        scores = scores.Skip(hourScores.Count());


        //        foreach (var score in hourScores)
        //        {
        //            var ship = ships.FirstOrDefault(a => a.ShipId.Equals(score.ShipId));
        //            var maxScore = scoreDict.Max(a => a.Value);

        //            var recentSupporterCountDict = allScores
        //                .Where(a => a.ShipId.Equals(score.ShipId)
        //                    && a.Timestamp <= score.Timestamp
        //                    && a.Timestamp > score.Timestamp.AddHours(-48))
        //                .GroupBy(a => a.Tier).ToArray()
        //                .Select(a => (a.Key, a
        //                    .Select(aa => aa.UserId)
        //                    .Distinct()
        //                    .Count()))
        //                .ToDictionary(t => t.Key, v => v.Item2);
        //            double recentSupporters = (recentSupporterCountDict.ContainsKey(0) ? (double)recentSupporterCountDict[0] : 0)
        //                + ((double)(recentSupporterCountDict.ContainsKey(1) ? (double)recentSupporterCountDict[1] : 0) * 2.0 / 5.0)
        //                + ((double)(recentSupporterCountDict.ContainsKey(2) ? (double)recentSupporterCountDict[2] : 0) * 1.0 / 5.0);

        //            //recentSupporters = Math.Max(recentSupporters, 1);

        //            //var recentScores = scores
        //            //    .Where(a => a.ShipId.Equals(score.ShipId)
        //            //        && a.Timestamp < score.Timestamp
        //            //        && a.Timestamp > score.Timestamp.AddHours(-48));

        //            //// Finite
        //            //if (recentSupporters > maxSupporters)
        //            //    maxSupporters = recentSupporters;

        //            //var lowestSupporterScoreMult = 6.66;
        //            //var highestSupporterScoreMult = 1.0;
        //            //var multPerSupporter = (lowestSupporterScoreMult - highestSupporterScoreMult) / (maxSupporters - 0.2);
        //            //var balanceMult = lowestSupporterScoreMult - (multPerSupporter * (recentSupporters - 0.2));
        //            //if (maxSupporters < 2)
        //            //    balanceMult = 1.0;
        //            //else if (maxSupporters == recentSupporters)
        //            //{

        //            //}
        //            //else if (recentSupporters <= 1.0)
        //            //{

        //            //}
        //            //var balancedScore = (double)score.PointsEarned * balanceMult;
        //            //if (double.IsNaN(balancedScore))
        //            //{

        //            //}

        //            //balanceMult = 7.66 - (.35 * ((double)ship.Supporters - 1.0));
        //            //balancedScore = (double)score.PointsEarned * balanceMult;


        //            //var balancedScore = ((double)score.PointsEarned * Math.Pow(1.0 / Math.Max(recentSupporters, 1), .75));

        //            var balanceFactor = 0;
        //            var balancedScore = ((double)score.PointsEarned / ((1.0 - balanceFactor) + (balanceFactor * Math.Max(recentSupporters, 1))));

        //            //var balanceMult = 6.66 - (.33 * ((double)ship.Supporters - 1.0));
        //            //var balancedScore = scoreDict[ship] > 0
        //            //    ? ((double)score.PointsEarned * Math.Pow(((double)maxScore / (double)scoreDict[ship]), 1.1))
        //            //    : score.PointsEarned;
        //            //var balancedScore = scoreDict[ship] > 0
        //            //    ? ((double)score.PointsEarned * Math.Pow((1.0 + (double)ship.TestPlace * .1), 1.1))
        //            //    : score.PointsEarned;

        //            if (double.IsInfinity(balancedScore) || balancedScore > 10000)
        //            {
        //                var scd = scoreDict[ship];
        //                var x = 0;
        //            }

        //            //var balancedScore = (double)score.PointsEarned * (1.0 + (((double)ship.Place - .5) * .1));
        //            if (ship != null)
        //                scoreDict[ship] += balancedScore;

        //            //// Update places
        //            //ships = scoreDict
        //            //    .OrderByDescending(a => a.Value)
        //            //    .Select(a => a.Key);
        //            //for (int j = 0; j < ships.Count(); j++)
        //            //{
        //            //    ships.ToArray()[j].TestPlace = j + 1;
        //            //}
        //        }

        //        var row = new List<object>() { i };
        //        row.AddRange(ships.Select(a => scoreDict[a].ToString()));
        //        sheetValues.Add(row);
        //        //File.AppendAllText("shipscores.xls", "\n" + i.ToString()
        //        //    + string.Join(", ", ships.Select(a => scoreDict[a])));
        //    }

        //    Console.WriteLine(maxSupporters);

        //    await sheetsService.UpdateDataAsync(sheetId, $"A1:YY{(24 * 30) + 1}", sheetValues as IList<IList<object>>);

        //    await ReplyAsync("Pushing...");

        //    // Update places
        //    ships = scoreDict
        //        .OrderByDescending(a => a.Value)
        //        .Select(a => a.Key);
        //    for (int j = 0; j < ships.Count(); j++)
        //    {
        //        ships.ToArray()[j].TestPlace = j + 1;
        //    }

        //    foreach (var ship in ships)
        //    {
        //        ship.TestScore = scoreDict[ship];
        //        await repo.UpdateShipAsync(connection, ship);
        //    }

        //    await ReplyResultAsync("Done!");

        //}


        [Command("scores")]
        [Alias("score")]
        [RequireRegistration]
        [Summary("Views your ships and how well they're doing!")]
        [ValidEventPeriods(EventPeriod.DuringEvent | EventPeriod.AfterEvent)]
        [Priority(1)]
        public async Task Score()
        {
            await Score(Context.User);
        }

        [Command("scores")]
        [Alias("score")]
        [RequireRegistration]
        [RequireSage]
        [Summary("Mod command to view another user's ships scores.")]
        [Priority(0)]
        public async Task Score(SocketUser user)
        {
            if (!(await userReg.GetOrDownloadAsync(user.Id.ToString())))
                throw new CommandException("That user isn't registered for this event!");
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetUserAsync(connection, user.Id.ToString());
            var username = (user as SocketGuildUser)?.Nickname ?? user.Username;
            var dbShips = await repo.GetUserShipsAsync(connection, dbUser);

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithThumbnailUrl(user.GetServerAvatarUrlOrDefault())
                .WithTitle($"Score Overview")
                .WithDescription(user == Context.User
                ? DialogueDict.Get("SCORES_VIEW_SELF")
                : DialogueDict.Get("SCORES_VIEW_OTHER", (user as SocketGuildUser)?.Nickname ?? user.Username))
                .WithFooter(new EmbedFooterBuilder()
                    .WithText(user.Id.ToString()));
            var spEmote = EmoteHelper.SPEmote;

            var guildSettings = await repo.GetGuildSettings(connection, config["ids:gyn"]);
            var leaderboardRevealed = guildSettings.LeaderboardAvailable;
            if (!leaderboardRevealed)
                embed.Description += " " + DialogueDict.Get("SCORES_VIEW_BLOCKED");

            //foreach (var ship in dbShips)
            //{
            //    embed.AddField($"__**{ship.GetDisplayName()}:**__",
            //        $"\n**#{(scoreboardRevealed ? ship.Place.ToString() : "??")}**" +
            //        $" with **{(ship.PointsEarned)} {spEmote}**" +
            //        $" (**{ship.PointsEarnedByUser}** from you)", true);
            //}

            //embed.AddField("\u200B",
            //    string.Join("\n\n", Enumerable.Range(0, 3)
            //    .Select(a => (UserShipTier)a)
            //    .Where(a => dbShips.Has(a))
            //    .Select(a => $"__**{dbShips.Get(a)?.GetDisplayName() ?? "None"}:**__" +
            //        $"\n**#{(scoreboardRevealed ? dbShips.Get(a).Place.ToString() : "??")}**" +
            //        $" with **{(dbShips.Get(a).PointsEarned)} {spEmote}**" +
            //        $" (**{dbShips.Get(a).PointsEarnedByUser}** from you)")), false);

            foreach (var ship in dbShips.OrderBy(a => a.Tier))
            {
                embed.AddField($"__**{ship.GetDisplayName()}:**__",
                    $"Currently **{(leaderboardRevealed ? (ship.Place.ToString() + MathHelper.GetPlacePrefix((int)ship.Place)) : "??th")}**" +
                    $" with **{(leaderboardRevealed ? ((int)Math.Round(ship.PointsEarned)).ToString() : "???")} {spEmote}**" +
                    $" (**{(int)Math.Round(ship.PointsEarnedByUser)}** from you)");
            }

            if (leaderboardRevealed)
            {
                embed.AddField($"Your {EmoteHelper.SPEmote} Totals for This Event:",
                    $"From Combined Achievements: **{(int)Math.Round(dbUser.PointsEarned)} {EmoteHelper.SPEmote}**" +
                    $"\nTotal Contributed to Pairings: **{(int)Math.Round(dbUser.PointsGivenToShips)} {EmoteHelper.SPEmote}**");
            }

            var recentScores = await repo.GetRecentScoresForUserAsync(connection, dbUser.UserId);
            if (recentScores.Any())
                embed.AddField("Your Recent Achievements (Last 24 Hours):",
                    string.Join("\n", recentScores
                    .Select(a => $"- {(a.Count > 1 ? $"**{a.Count}x**" : "")} {a.Description}")), true);

            var scoreStrs = Enumerable.Range(0, 3)
                .Select(a => !dbShips.Has((UserShipTier)a)
                    ? null
                    : (leaderboardRevealed
                        ? "#" + dbShips.Get((UserShipTier)a).Place.ToString()
                        : "#??"))
                .ToArray();
            var imageFIle = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips, scoreTexts: scoreStrs);
            embed.WithAttachedImageUrl(imageFIle);
            await ReplyAsync(embed: embed.Build());
        }

        [Command("shipscore")]
        [Alias("shipscores")]
        [RequireRegistration]
        [ValidEventPeriods(EventPeriod.DuringEvent | EventPeriod.AfterEvent)]
        [Summary("Views the place and LP count of a particular pairing!")]
        public async Task ShipScore([Remainder] string shipName)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var guildSettings = await repo.GetGynGuildSettings(connection, config);
            if (!guildSettings.LeaderboardAvailable && !Context.User.IsGYNSage(config))
                throw new CommandException(DialogueDict.Get("SHIP_SCORES_EARLY"));

            var dbCharacters = await repo.GetAllCharactersAsync(connection);
            var shipResult = await RegistrationSession.ParseShipAsync(connection, repo, shipName, dbCharacters);
            if (!shipResult.IsSuccess)
                throw new CommandException(shipResult.ErrorMessage);
            var ship = shipResult.Value;
            var validationResult = await RegistrationSession.ValidateShipAsync(connection, shipResult.Value, dbCharacters);
            if (!validationResult.IsSuccess)
                throw new CommandException(DialogueDict.Get("SHIP_SCORES_INVALID"));

            var gyn = Context.Client.GetGyn(config);
            var supporterLines = (await repo.GetUserShipsForShipAsync(connection, ship.ShipId))
                .Where(a => gyn.GetUser(ulong.Parse(a.UserId)) != null)
                .OrderBy(a => a.Tier)
                .Select(a => $"{gyn.GetUser(ulong.Parse(a.UserId)).Mention} ({(UserShipTier)a.Tier})")
                .ToList();
            //$", **{a.PointsEarnedByUser} {EmoteHelper.SPEmote}** earned for them");
            var shiptRarityMult = await repo.GetScoreBalanceMultForShip(connection, ship.ShipId, DateTime.Now, null, -1);

            // build description for ship score
            string desc;
            if (ship.Supporters > 0 || ship.PointsEarned > 0)
            {
                desc = DialogueDict.Get("SHIP_SCORES_DESC", ship.GetDisplayName(),
                    ((int)ship.Place).ToString() + MathHelper.GetPlacePrefix((int)ship.Place).ToString(), (int)Math.Round(ship.PointsEarned), supporterLines.Count(), shiptRarityMult);
                if (guildSettings.CatchupEnabled)
                {
                    var shipCatchupMult = await repo.GetScoreCatchupMultForShip(connection, ship.ShipId);
                    if (!MathHelper.Approximately(shipCatchupMult, 1m, .01m))
                        desc += "\n\n" + DialogueDict.Get("SHIP_SCORES_DESC_CATCHUP", shipCatchupMult);
                }
                desc += "\n\n" + DialogueDict.Get("SHIP_SCORES_DESC_RARITY_NOTE");
            }
            else
                desc = DialogueDict.Get("SHIP_SCORES_NO_POINTS");


            var shipImageFile = await shipImageGenerator.WriteShipImageAsync(ship);
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithAttachedThumbnailUrl(shipImageFile)
                .WithTitle($"Ship Overview: {ship.GetDisplayName()}")
                .WithDescription(desc);

            if (supporterLines.Any())
            {

                var fieldValues = new List<string>();
                fieldValues.Add("");
                for (int i = 0; i < supporterLines.Count(); i++)
                {
                    var supporterLine = "\n" + supporterLines[i];
                    var valueIndex = fieldValues.Count - 1;
                    if ((fieldValues[valueIndex] + supporterLine).Length > 1000)
                        fieldValues.Add(supporterLine);
                    else
                        fieldValues[valueIndex] += supporterLine;
                }

                embed.AddField($"{ship.GetDisplayName()}'s Current Active Supporters:", string.Join("\n", fieldValues[0]), true);
                for (int i = 1; i < fieldValues.Count; i++)
                {
                    embed.AddField("\u200B", string.Join("\n", fieldValues[i]), true);
                }
            }

            await Context.Channel.SendFileAsync(shipImageFile.Stream, shipImageFile.FileName, embed: embed.Build());
        }
    }
}