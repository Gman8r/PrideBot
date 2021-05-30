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
using PrideBot.Registration;
using PrideBot.Game;

namespace PrideBot.Modules
{
    [Name("Registration")]
    public class RegistrationModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly ShipImageGenerator shipImageGenerator;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly UserRegisteredCache userReg;

        public override int HelpSortOrder => -50;

        public RegistrationModule(ModelRepository modelRepository, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, DiscordSocketClient client, ScoringService scoringService, UserRegisteredCache userReg)
        {
            this.repo = modelRepository;
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
            this.scoringService = scoringService;
            this.userReg = userReg;
        }

        [Command("register")]
        [Summary("Allows you to register with for the event, or change your setup.")]
        [RequireSingleSession]
        [ValidEventPeriods(EventPeriod.BeforeEvent | EventPeriod.DuringEvent)]
        public async Task Register()
        {
            await new RegistrationSession(await Context.User.GetOrCreateDMChannelAsync(), Context.User, config, shipImageGenerator, repo, client,
                new TimeSpan(0, 10, 0), Context.Message, scoringService, userReg)
                .PerformSessionAsync();
        }

        [Command("setbackground")]
        [Alias("setbg", "changebackground", "changebg")]
        [Summary("Allows you to register with for the event, or change your setup.")]
        [RequireRegistration]
        [ValidEventPeriods(EventPeriod.BeforeEvent | EventPeriod.DuringEvent)]
        public async Task SetBackground(int bgIndex = 0)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetOrCreateUserAsync(connection, Context.User.Id.ToString());
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithTitle("Background Config");
            if (bgIndex == 0)
            {
                var bgImagePath = await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser);
                embed.Description = DialogueDict.Get("SET_BACKGROUND_LIST", config.GetDefaultPrefix());
                embed.ImageUrl = config.GetRelativeHostPathWeb(bgImagePath);
            }
            else
            {
                var fileCount = Directory.GetFiles("Assets/Backgrounds").Length;
                if (bgIndex <= 0 || bgIndex > fileCount)
                    throw new CommandException(DialogueDict.GetNoBrainRot("SET_BACKGROUND_ERROR"));
                dbUser.CardBackground = bgIndex;
                await repo.UpdateUserAsync(connection, dbUser);

                var dbShips = await repo.GetUserShipsAsync(connection, dbUser.UserId);
                var shipImagePath = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips);
                embed.Description = DialogueDict.Get("SET_BACKGROUND_CHANGED");
                embed.ImageUrl = config.GetRelativeHostPathWeb(shipImagePath);
            }
            await ReplyAsync(embed: embed.Build());
        }
    }
}