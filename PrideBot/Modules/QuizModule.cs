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
    [Name("Daily Quiz")]
    public class QuizModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly PlushieService plushieService;

        public QuizModule(ModelRepository modelRepository, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, PlushieService plushieService)
        {
            this.repo = modelRepository;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.plushieService = plushieService;
        }

        [Command("takequiz")]
        [Summary("Attempt today's daily quiz question!")]
        [RequireRegistration]
        [RequireSingleSession]
        [DontInterruptCutsceneAtribute]
        [ValidEventPeriods(EventPeriod.DuringEvent)]
        public async Task TakeQuiz()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var guildSettings = await repo.GetOrCreateGuildSettingsAsync(connection, client.GetGyn(config).Id.ToString());
            if (!guildSettings.QuizOpen)
            {
                throw new CommandException(DialogueDict.GetNoBrainRot("QUIZ_CLOSED"));
            }
            var quizLog = await repo.GetOrCreateQuizLogAsync(connection, Context.User.Id.ToString(), guildSettings.QuizDay.ToString());
            if (quizLog.Attempted)
            {
                if (Context.User.IsGYNSage(config))
                    await ReplyAsync("I'd yell at you here bc you've taken the quiz but you're a sage so nvm YEET!");
                else
                    throw new CommandException(DialogueDict.GetNoBrainRot("QUIZ_ATTEMPTED"));
            }
            await connection.CloseAsync();
            await new QuizSession(await Context.User.CreateDMChannelAsync(), Context.User, config, repo, client,
                new TimeSpan(0, 10, 0), Context.Message, scoringService, quizLog, guildSettings, plushieService)
                .PerformSessionAsync();
        }

        [Command("takequizday")]
        [Summary("Mod function to take the quiz for a particular day for the month, for yourself or someone else.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [RequireSage]
        public async Task TakeQuizDay(int day, SocketUser user = null)
        {
            user ??= Context.User;
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var guildSettings = await repo.GetOrCreateGuildSettingsAsync(connection, client.GetGyn(config).Id.ToString());
            var quizLog = await repo.GetOrCreateQuizLogAsync(connection, user.Id.ToString(), day.ToString());
            await connection.CloseAsync();
            await new QuizSession(await user.CreateDMChannelAsync(), user, config, repo, client,
                new TimeSpan(0, 5, 0), Context.Message, scoringService, quizLog, guildSettings, plushieService)
                .PerformSessionAsync();
        }
    }
}