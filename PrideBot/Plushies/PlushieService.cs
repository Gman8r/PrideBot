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
        readonly UserRegisteredCache userReg;
        readonly ShipImageGenerator shipImageGenerator;

        public PlushieService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client, ScoringService scoringService, UserRegisteredCache userReg, ShipImageGenerator shipImageGenerator)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
            this.scoringService = scoringService;
            this.userReg = userReg;
            this.shipImageGenerator = shipImageGenerator;
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


        public async Task GiveUserPlushie(SqlConnection connection, IMessageChannel channel, IUser user, string plushieId)
        {
            var chars = await repo.GetAllCharactersAsync(connection);
            var character = chars
                .FirstOrDefault(a => a.PlushieId?.ToUpper().Trim().Equals(plushieId.ToUpper().Trim()) ?? false);
            var result = await repo.AttemptAddUserPlushieAsync(connection, user.Id.ToString(), null, user.Id.ToString(), character.PlushieId, character.CharacterId,
                GameHelper.GetEventDay(config), 0m, PlushieTransaction.Drawn);
            var commandResult = result.CheckErrors();
            if (!commandResult.IsSuccess)
                throw new CommandException(commandResult.ErrorMessage);
        }


        public async Task GiveUserPlushieCharacter(SqlConnection connection, IMessageChannel channel, IUser user, string characterId)
        {
            var character = await repo.GetCharacterAsync(connection, characterId);
            var result = await repo.AttemptAddUserPlushieAsync(connection, user.Id.ToString(), null, user.Id.ToString(), character.PlushieId, characterId,
                GameHelper.GetEventDay(config), 0m, PlushieTransaction.Drawn);
            var commandResult = result.CheckErrors();
            if (!commandResult.IsSuccess)
                throw new CommandException(commandResult.ErrorMessage);
        }

        public async Task ActivateUserPlushie(SqlConnection connection, SocketGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IServiceProvider provider, IDiscordInteraction interaction = null)
        {
            var session = new PlushieEffectSession(channel, user, config, client, new TimeSpan(0, 10, 0), null, repo, imageService, scoringService, connection,
                userPlushie, provider, this, interaction);
            await session.PerformSessionAsync();
        }

        public async Task HandleTraderAwardAsync(SqlConnection connection, UserPlushie userPlushie, IMessageChannel channel)
        {
            if (string.IsNullOrWhiteSpace(userPlushie.OriginalUserId) || userPlushie.OriginalUserId.Equals(userPlushie.UserId))
                return;
            var gyn = client.GetGyn(config);
            if (gyn == null)
                return;
            var owner = gyn.GetUser(ulong.Parse(userPlushie.OriginalUserId));
            if (owner == null)
                return;
            if (!(await userReg.GetOrDownloadAsync(userPlushie.OriginalUserId)))
                return;

            var msgText = DialogueDict.Get("PLUSHIE_TRADER_BONUS", owner.Mention);
            var msg = await channel.SendMessageAsync(msgText, allowedMentions: AllowedMentions.None);

            await scoringService.AddAndDisplayAchievementAsync(connection, owner, "TRADER_BONUS", client.CurrentUser, DateTime.Now,
                null, applyPlushies: false, titleUrl: msg.Channel is IGuildChannel ? msg.GetJumpUrl() : null);
        }
        
        public async Task PawnUserPlushie(IMessageChannel channel, SocketUser user, int userPlushieId, DiscordSocketClient client, TimeSpan timeout, IDiscordInteraction interaction = null, IMessage originMessage = null)
        {
            var session = new PlushiePawnSession(channel, user, userPlushieId, config, client, timeout, scoringService, repo, shipImageGenerator, this, interaction, originMessage);
            await session.PerformSessionAsync();
        }

    }
}
