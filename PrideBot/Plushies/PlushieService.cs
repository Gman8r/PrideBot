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
using PrideBot.Registration;

namespace PrideBot.Plushies
{
    public class PlushieService
    {

        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly PlushieImageService imageService;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;

        public PlushieService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client, ScoringService scoringService)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
            this.scoringService = scoringService;
        }


        public async Task DrawPlushie(SqlConnection connection, IMessageChannel channel, SocketUser user, IDiscordInteraction interaction = null, bool isRegistration = false)
        {
            var session = new PlushieDrawSession(channel, user, config, repo, client, TimeSpan.FromMinutes(10), null, imageService, interaction, isRegistration);
            await session.PerformSessionAsync();
        }

        public async Task TradePlushieInSession(SqlConnection connection, IMessageChannel channel, SocketUser user, int selectedPlushieId1, IServiceProvider provider, IDiscordInteraction interaction = null)
        {
            var session = new PlushieTradeSession(channel, user, config, repo, client,
                new TimeSpan(0, 10, 0), null, imageService, interaction, provider, connection, selectedPlushieId1);
            await session.PerformSessionAsync();
        }


        public async Task GiveUserPlushie(SqlConnection connection, IMessageChannel channel, IUser user, string characterId)
        {
            var character = await repo.GetCharacterAsync(connection, characterId);
            var result = await repo.AttemptAddUserPlushieAsync(connection, user.Id.ToString(), null, user.Id.ToString(), character.PlushieId, characterId,
                GameHelper.GetEventDay(), 0m, PlushieTransaction.Drawn);
            result.CheckErrors();
        }

        public async Task ActivateUserPlushie(SqlConnection connection, SocketGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction = null)
        {
            var session = new PlushieEffectSession(channel, user, config, client, new TimeSpan(0, 10, 0), null, repo, imageService, scoringService, connection,
                userPlushie, interaction);
            await session.PerformSessionAsync();
        }

    }
}
