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
using PrideBot.Plushies;

namespace PrideBot.Modules
{
    [Name("Plushie")]
    [RequireGyn]
    public class PlushieModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly PlushieMenuService menuService;
        readonly PlushieService plushieService;

        public PlushieModule(ModelRepository modelRepository, IConfigurationRoot config, DiscordSocketClient client, PlushieMenuService plushieMenuService, PlushieService plushieService)
        {
            this.repo = modelRepository;
            this.config = config;
            this.client = client;
            this.menuService = plushieMenuService;
            this.plushieService = plushieService;
        }

        [Command("plushies")]
        [Summary("Make da menu")]
        //[RequireRegistration]
        //[RequireSingleSession]
        //[ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task Plushie()
        {
            await menuService.PostPlushieMenuAsync(Context.User as IGuildUser, Context.Channel, new List<UserPlushie>());
        }

        [Command("getplushie")]
        [Summary("Get da plushie")]
        //[RequireRegistration]
        //[RequireSingleSession]
        //[ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task DrawPlushie()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await plushieService.DrawPlushie(connection, Context.Channel, null);
        }
    }
}