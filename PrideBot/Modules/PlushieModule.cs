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
        [Alias("fumorama")]
        [Summary("Shows your plushies! 🧸")]
        [ValidEventPeriods(EventPeriod.DuringEvent | EventPeriod.BeforeEvent)]
        [DontInterruptCutsceneAtribute]
        [RequireRegistration]
        //[RequireSingleSession]
        public async Task Plushie(SocketGuildUser user = null)
        {
            user ??= Context.User as SocketGuildUser;
            var viewingOther = user.Id != Context.User.Id;
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await menuService.PostPlushieMenuAsync(connection, user as IGuildUser, Context.Channel, viewingOther: viewingOther);
        }

        [Command("getplushie")]
        [DontInterruptCutsceneAtribute]
        [Summary("Get a new plushie! 🧸")]
        [RequireRegistration]
        [RequireSingleSession]
        [Priority(0)]
        [ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task DrawPlushie()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await plushieService.DrawPlushie(connection, Context.Channel, Context.User);
            await Plushie();
        }

        [Command("getplushienow")]
        [Summary("Admin command !!")]
        [RequireSage]
        [Priority(1)]
        public Task GetPlushieAdmin(string characterId)
            => GivePlushie(Context.User as SocketGuildUser, characterId);

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
                await plushieService.GiveUserPlushieCharacter(connection, Context.Channel, user, characterId);
            }
            catch (CommandException e)
            {
                throw;
            }
            catch(Exception e)
            {
                await plushieService.GiveUserPlushie(connection, Context.Channel, user, characterId);
            }
            await ReplyResultAsync("Done!");
            await Plushie(user);
        }

        [Command("clearplushies")]
        [Summary("Admin command !!")]
        [RequireSage]
        //[RequireSingleSession]
        //[ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task ClearPlushies (SocketGuildUser user = null)
        {
            user ??= Context.User as SocketGuildUser;
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await repo.ClearUserPlushiesAsync(connection, user.Id.ToString());
            await ReplyResultAsync("Done!");
            await Plushie();
        }
    }
}