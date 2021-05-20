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

namespace PrideBot.Modules
{
    [Name("Daily Quiz")]
    public class QuizModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;

        public QuizModule(ModelRepository modelRepository, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService)
        {
            this.repo = modelRepository;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
        }

        [Command("takequiz")]
        [Summary("Attempt today's daily quiz question!")]
        [RequireRegistration]
        public async Task Quiz()
        {
            await new QuizSession(await Context.User.GetOrCreateDMChannelAsync(), Context.User, config, repo, client,
                new TimeSpan(0, 5, 0), Context.Message, scoringService)
                .PerformSessionAsync();
        }
    }
}