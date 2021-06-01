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


        public GameModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, ShipImageGenerator shipImageGenerator, DiscordSocketClient client, ScoringService scoringService)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
            this.scoringService = scoringService;
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
                    throw new CommandException($"{username} hasn't registered and configured {pronoun} pairings yet!");
                }
            }

            var dbShips = await repo.GetUserShipsAsync(connection, dbUser);

            var imagePath = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips);
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath))
                .WithThumbnailUrl(user.GetAvatarUrlOrDefault())
                .WithTitle($"User Overview")
                .WithDescription(isSelf ? "Here's who you're supporting!" : $"Here's who {user.Mention} is supporting!")
                .WithFooter(new EmbedFooterBuilder()
                    .WithText(user.Id.ToString()));

            embed.AddField("Ships Supported:",
                string.Join("\n", Enumerable.Range(0, 3)
                .Select(a => (UserShipTier)a)
                .Where(a => dbShips.Has(a))
                .Select(a => $"{EmoteHelper.GetShipTierEmoji(a)} **{a}** Pairing: **{dbShips.Get(a)?.GetDisplayName() ?? "None"}**" +
                    $" {((dbShips.Get(a).ScoreRatio == 1m || !dbShips.Has(a)) ? "" : $" ({GameHelper.GetPointPercent(dbShips.Get(a).ScoreRatio)}% SP)")}")));

            await ReplyAsync(embed: embed.Build());
        }

        [Command("scores")]
        [Alias("score")]
        [RequireRegistration]
        [Summary("Views your ships and how well they're doing!")]
        public async Task SCore()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetUserAsync(connection, Context.User.Id.ToString());
            var username = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            var dbShips = await repo.GetUserShipsAsync(connection, dbUser);

            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithThumbnailUrl(Context.User.GetAvatarUrlOrDefault())
                .WithTitle($"Score Overview")
                .WithDescription("Here's some stats on how your ships are doing!")
                .WithFooter(new EmbedFooterBuilder()
                    .WithText(Context.User.Id.ToString()));
            var spEmote = EmoteHelper.SPEmote;

            var guildSettings = await repo.GetGuildSettings(connection, config["ids:gyn"]);
            var leaderboardRevealed = guildSettings.LeaderboardAvailable;
            if (!leaderboardRevealed)
                embed.Description += " But HMM I can only see so much right now, it'll take some time before I can get you the full picture!";

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

            foreach (var ship in dbShips)
            {
                embed.AddField($"__**{ship.GetDisplayName()}:**__",
                    $"Currently **{(leaderboardRevealed ? (ship.Place.ToString() + GetPlacePrefix((int)ship.Place)) : "??th")}**" +
                    $" with **{(leaderboardRevealed ? ship.PointsEarned.ToString() : "???")} {spEmote}**" +
                    $" (**{ship.PointsEarnedByUser}** from you)");
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
            var imagePath = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips, scoreTexts: scoreStrs);
            embed.ImageUrl = config.GetRelativeHostPathWeb(imagePath);
            await ReplyAsync(embed: embed.Build());
        }

        string GetPlacePrefix(int num)
        {
            num %= 100;
            switch(num)
            {
                case (0):
                    return "th";
                case (1):
                    return "st";
                case (2):
                    return "nd";
                case (3):
                    return "rd";
            }
            if (num <= 20)
                return "th";
            return GetPlacePrefix(num % 10);

        }
    }
}