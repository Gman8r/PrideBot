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
        [ValidEventPeriods(EventPeriod.DuringEvent)]
        //[RequireRegistration]
        [RequireSingleSession]
        public async Task Plushie()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await menuService.PostPlushieMenuAsync(connection, Context.User as IGuildUser, Context.Channel);
        }

        [Command("getplushie")]
        [Summary("Get da plushie")]
        [RequireRegistration]
        [RequireSingleSession]
        [ValidEventPeriods(EventPeriod.DuringEvent | EventPeriod.BeforeEvent)]
        public async Task DrawPlushie()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await plushieService.DrawPlushie(connection, Context.Channel, Context.User);
        }

        [Command("giveplushie")]
        [Summary("Admin command !!")]
        [RequireSage]
        //[RequireSingleSession]
        //[ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task GivePlushie(SocketGuildUser user, string characterId)
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            try
            {
                await plushieService.GiveUserPlushie(connection, Context.Channel, user, characterId);
            }
            catch (Exception e)
            {
                var x = 0;
            }
            await ReplyResultAsync("Done!");
        }
    }
}