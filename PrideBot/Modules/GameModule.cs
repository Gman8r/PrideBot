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
        [Summary("Views your chosen pairings.")]
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
            var embed = EmbedHelper.GetEventEmbed(user, config, userInThumbnail: true)
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath))
                .WithTitle($"Overview for {user.Username}#{user.Discriminator}")
                .WithDescription(isSelf ? "Here's who you're supporting!" : $"Here's who {user.Mention} is supporting!");
            embed.AddField("Ships Supported:",
                string.Join("\n", Enumerable.Range(0, 3)
                .Select(a => (UserShipTier)a)
                .Where(a => dbShips.Has(a))
                .Select(a => $"{EmoteHelper.GetShipTierEmoji(a)} **{a}** Pairing: **{dbShips.Get(a)?.GetDisplayName() ?? "None"}**" +
                    $" {((dbShips.Get(a).ScoreRatio == 1m || !dbShips.Has(a)) ? "" : $" ({GameHelper.GetPointPercent(dbShips.Get(a).ScoreRatio)}% SP)")}")));

            await ReplyAsync(embed: embed.Build());
        }
    }
}