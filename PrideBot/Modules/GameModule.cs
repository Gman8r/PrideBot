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


        public GameModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, ShipImageGenerator shipImageGenerator)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.shipImageGenerator = shipImageGenerator;
        }

        [Command("ships")]
        [Summary("Views your chosen ships")]
        public async Task Ships([Remainder] string command = "")
        {
            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetUserAsync(connection, Context.User.Id.ToString());
            if (!(dbUser?.ShipsSelected ?? false))
                throw new CommandException($"You haven't configured your ships yet! Register with `{config.GetDefaultPrefix()}register` first.");

            var dbShips = await repo.GetUserShipsAsync(connection, dbUser);

            var imagePath = await shipImageGenerator.WriteUserAvatarAsync(dbUser, dbShips);
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath))
                .WithTitle("Ship Overview")
                .WithDescription("Here's who you're supporting!");
            embed.AddField("Ships Supported:",
                string.Join("\n", Enumerable.Range(0, 3)
                .Select(a => (UserShipTier)a)
                .Select(a => $"{EmoteHelper.GetShipTierEmoji(a)} **{a}** Pairing: **{dbShips.Get(a).Character1First}X{dbShips.Get(a).Character2First}**" +
                $" {(GameHelper.GetPointFraction(a) == 1m ? "" : $" ({GameHelper.GetPointFraction(a)}x SP)")}")));

            await ReplyAsync(embed: embed.Build());
        }

    }
}