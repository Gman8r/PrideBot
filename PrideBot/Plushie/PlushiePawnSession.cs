using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Registration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Plushie
{
    class PlushiePawnSession : Session
    {
        private readonly SocketInteraction originInteraction;
        private readonly ModelRepository repo;
        private readonly UserPlushie plushie;

        public PlushiePawnSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout,
            SocketInteraction originInteraction,
            IMessage originMessage = null, ModelRepository repo = null, UserPlushie plushie = null) : base(channel, user, config, client, timeout)
        {
            this.originInteraction = originInteraction;
            this.repo = repo;
            this.plushie = plushie;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var dbCharacters = await repo.GetAllCharactersAsync(connection);

            // TODO
            //var shipResult = await RegistrationSession.ParseShipAsync(connection, repo, shipName, dbCharacters);
            //if (!shipResult.IsSuccess)
            //    throw new CommandException(shipResult.ErrorMessage);
            //var ship = shipResult.Value;
            //var validationResult = await RegistrationSession.ValidateShipAsync(connection, shipResult.Value, dbCharacters);
            //if (!validationResult.IsSuccess)
            //    throw new CommandException(DialogueDict.Get("SHIP_SCORES_INVALID"));
        }
    }
}
