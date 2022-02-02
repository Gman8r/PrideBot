using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Game;
using PrideBot.Models;
using PrideBot.Registration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Plushies
{
    class PlushiePawnSession : Session
    {
        private readonly ModelRepository repo;
        private readonly IDiscordInteraction interaction;
        private readonly ScoringService scoringService;
        private readonly int userPlushieId;
        private readonly ShipImageGenerator shipImageGenerator;

        public PlushiePawnSession(IMessageChannel channel, SocketUser user, int userPlushieId, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout, ScoringService scoringService, ModelRepository repo, ShipImageGenerator shipImageGenerator, IDiscordInteraction interaction = null, IMessage originMessage = null) : base(channel, user, config, client, timeout)
        {
            this.repo = repo;
            this.interaction = interaction;
            this.scoringService = scoringService;
            this.userPlushieId = userPlushieId;
            this.shipImageGenerator = shipImageGenerator;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var dbCharacters = await repo.GetAllCharactersAsync(connection);
            var plushie = await repo.GetUserPlushieAsync(connection, userPlushieId);

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Sell Your Goods")
                .WithDescription(DialogueDict.Get("PAWN_PLUSHIE_SHIP", plushie.CharacterName));

            var components = new ComponentBuilder()
                .WithButton("Enter The Pairing as Text [Character One X Character Two]",
                    emote: new Emoji("ℹ"), style: ButtonStyle.Secondary, customId: "TEXTINSTRUCTIONS", disabled: true)
                .WithButton("Nevermind, Not Now.", "NO", ButtonStyle.Secondary, new Emoji("❌"));

            var response = await SendAndAwaitResponseAsync(embed: embed, interaction: interaction, components: components);

            if (response.IsNo)
            {
                MarkCancelled("Ok well keep it I guess! 🙌");
                throw new OperationCanceledException();
            }

            var shipMessage = response.MessageResponse;

            // validate ship and such
            var shipResult = await RegistrationSession.ParseShipAsync(connection, repo, response.MessageResponse.Content, dbCharacters);

            if (!shipResult.IsSuccess)
            {
                // yell
                MarkCancelled(shipResult.ErrorMessage);
                throw new OperationCanceledException();
            }
            var ship = shipResult.Value;

            if (!ship.CharacterId1.Equals(plushie.CharacterId, StringComparison.OrdinalIgnoreCase)
                && !ship.CharacterId2.Equals(plushie.CharacterId, StringComparison.OrdinalIgnoreCase))
            {
                // yell
                MarkCancelled(DialogueDict.Get("PAWN_PLUSHIE_NO_CHAR"));
                throw new OperationCanceledException();
            }

            var validationResult = await RegistrationSession.ValidateShipAsync(connection, shipResult.Value, dbCharacters);
            if (!validationResult.IsSuccess)
            {
                // yell
                MarkCancelled(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));
                throw new OperationCanceledException();
            }

            // but are they like SURE??
            var shipImage = await shipImageGenerator.WriteShipImageAsync(ship);
            embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Sell To This Pairing?")
                .WithDescription(DialogueDict.Get("PAWN_PLUSHIE_CONFIRM", ship.GetDisplayName()))
                .WithAttachedThumbnailUrl(shipImage);
            var yesNoComponents = new ComponentBuilder()
                .WithButton("Yeppers!", "YES", ButtonStyle.Success, new Emoji("👍"))
                .WithButton("No, Not That One Actually", "NO", ButtonStyle.Secondary, new Emoji("❌"));

            await SendAndAwaitNonTextResponseAsync(embed: embed, components: yesNoComponents, file: shipImage);

            if (response.IsNo)
            {
                // yell
                MarkCancelled("Ohh ok, try it again then! ♻");
                throw new OperationCanceledException();
            }

            // add the achievement
            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PAWN_PLUSHIE", client.CurrentUser, DateTime.Now, response.MessageResponse,
                overrideShip: ship.ShipId, titleUrl: shipMessage.GetJumpUrl());

            // wow!!
            embed = EmbedHelper.GetEventEmbed(user, config)
                .WithDescription(DialogueDict.Get("PAWN_PLUSHIE_COMPLETE"));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // goodbye card u were nice
            await repo.AttemptRemoveUserPlushieAsync(connection, userPlushieId, PlushieTransaction.Pawn, DateTime.Now);

        }
    }
}
