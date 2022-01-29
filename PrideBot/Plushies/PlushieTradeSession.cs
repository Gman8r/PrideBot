using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Plushies
{
    class PlushieTradeSession : Session
    {
        private readonly ModelRepository repo;
        private readonly SqlConnection connection;
        private readonly PlushieImageService imageService;
        private readonly IDiscordInteraction interaction;
        private readonly int userPlushieId1;
        private readonly IServiceProvider provider;

        private Phase currentPhase = Phase.U1ChooseOtherUser;
        private IUser user2 = null;

        private enum Phase
        {
            U1ChooseOtherUser,
            U2ChooseCard,
            ConfirmOrCancel,
            Confirmed
        }


        public PlushieTradeSession(IMessageChannel channel, SocketUser user, IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, PlushieImageService imageService, IDiscordInteraction interaction, IServiceProvider provider, SqlConnection connection, int userPlushieId1) : base(channel, user, config, client, timeout, originmessage)
        {
            this.repo = repo;
            this.imageService = imageService;
            this.interaction = interaction;
            this.provider = provider;
            this.connection = connection;
            this.userPlushieId1 = userPlushieId1;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            var userId = user.Id.ToString();

            // prompt user 2
            var userPlushie1 = await repo.GetUserPlushieAsync(connection, userPlushieId1);
            var imageFile = await imageService.WritePlushieImageAsync(userPlushie1);
            var embed = GetEmbed()
                .WithTitle($"Trade {userPlushie1.CharacterName}")
                .WithDescription(DialogueDict.Get("PLUSHIE_TRADE_WHO"))
                .WithAttachedImageUrl(imageFile);
            var components = new ComponentBuilder()
                .WithButton("Enter Your Response In Text", "CHOOSE", ButtonStyle.Secondary, emote: new Emoji("💬"), disabled: true)
                .WithButton("Nevermind, Don't Wanna", "NO", ButtonStyle.Secondary, emote: new Emoji("❌"));
            var response = await SendAndAwaitResponseAsync(user.Mention, embed: embed, components: components, file: imageFile, interaction: interaction);

            if (response.IsNo)
            {
                MarkCancelled("Ok Laters!");
                throw new OperationCanceledException();
            }

            // parse user 2
            var typeReader = new UserTypeReader<IUser>();
            var context = new CommandContext(client, response.MessageResponse as IUserMessage);
            var userResult = await typeReader.ReadAsync(context, response.MessageResponse.Content, provider);
            currentPhase = Phase.U2ChooseCard;

            if (!userResult.IsSuccess)
            {
                MarkCancelled(DialogueDict.Get("WRONG_USER"));
                throw new OperationCanceledException();
            }
            user2 = (IUser)userResult.BestMatch;
            if (user2.IsBot)
            {
                MarkCancelled(DialogueDict.Get(user2.Id == client.CurrentUser.Id ? "WRONG_SELF" : "WRONG_BOT"));
                throw new OperationCanceledException();
            }
            var user2Id = user2.Id.ToString();

            // prompt user 2 for their plushie to trade
            var user2Plushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, user2Id);
            if (!user2Plushies.Any())
            {
                MarkCancelled(DialogueDict.Get("PLUSHIE_TRADE_NONE", user2.Mention));
                throw new OperationCanceledException();
            }
            var plushie2 = await repo.GetUserPlushieAsync(connection, userPlushieId1);
            imageFile = await imageService.WritePlushieCollectionImageAsync(user2Plushies);
            embed = GetEmbed()
                .WithTitle($"Trade For {userPlushie1.CharacterName}")
                .WithDescription(DialogueDict.Get("PLUSHIE_TRADE_CHOOSE_OTHER", user.Mention, userPlushie1.CharacterName))
                .WithAttachedImageUrl(imageFile);
            var selectMenuOptions = user2Plushies
                .Select(a => new SelectMenuOptionBuilder(a.CharacterName, a.UserPlushieId.ToString(), a.Name, isDefault: false))
                .ToList();
            components = new ComponentBuilder()
                .WithSelectMenu("CHOOSE_PLUSHIE", selectMenuOptions, "Choose From Your Plushies!")
                .WithButton("Cancel (Either Party Can Press)", "NO", ButtonStyle.Secondary, emote: new Emoji("❌"));
            response = await SendAndAwaitNonTextResponseAsync(user2.Mention, embed: embed, components: components, file: imageFile, interaction: response.InteractionResponse);

            if (response.IsNo)
            {
                MarkCancelled("Ok Laters!");
                throw new OperationCanceledException();
            }
            currentPhase = Phase.ConfirmOrCancel;

            var customId = response.InteractionResponse.Data.Values.FirstOrDefault();
            var userPlushie2 = user2Plushies.FirstOrDefault(a => a.UserPlushieId.ToString().Equals(customId));


            // have both users confirm
            var bothPlushies = new List<UserPlushie>() { userPlushie1, userPlushie2 };
            imageFile = await imageService.WritePlushieCollectionImageAsync(bothPlushies);

            var user1Confirmed = false;
            var user2Confirmed = false;
            var shouldEditEmbed = false;
            IUserMessage promptMessage = null;
            // loop while both users haven't confirmed. Edit post as necessary.
            do
            {

                embed = GetEmbed()
                    .WithTitle($"Confirm Trade 🔃")
                    .WithDescription(DialogueDict.Get("PLUSHIE_TRADE_CONFIRM"))
                    .WithAttachedImageUrl(imageFile);
                selectMenuOptions = user2Plushies
                    .Select(a => new SelectMenuOptionBuilder(a.CharacterName, a.UserPlushieId.ToString(), a.Name))
                    .ToList();
                components = new ComponentBuilder()
                    .WithButton("Accept", "YES", ButtonStyle.Secondary, emote: new Emoji("👍"))
                    .WithButton("Do Not Accept", "NO", ButtonStyle.Secondary, emote: new Emoji("👎"));
                if (promptMessage == null)
                {
                    response = await SendAndAwaitNonTextResponseAsync(user.Mention + " " + user2.Mention, embed: embed, components: components, file: imageFile, interaction: response.InteractionResponse, disableComponents: false);
                    promptMessage = response.BotMessage;
                }
                else if (shouldEditEmbed)
                {
                    embed.Description += "\n";
                    if (user1Confirmed)
                        embed.Description += $"\n{user.Mention} has Confirmed! ✅";
                    if (user2Confirmed)
                        embed.Description += $"\n{user2.Mention} has Confirmed! ✅";
                    await response.BotMessage.ModifyAsync(a => a.Embed = embed.Build());
                    response.IsEntered = false;
                    response = await AwaitCurrentResponseAsync();
                }

                // check for cancel
                if (response.IsNo)
                {
                    embed.Description = DialogueDict.Get("PLUSHIE_TRADE_CONFIRM") + "\n\nTrade was cancelled 🙁";
                    response.BotMessage.ModifyAsync(a => a.Embed = embed.Build()).GetAwaiter();
                    MarkCancelled(DialogueDict.Get("PLUSHIE_TRADE_CANCELLED"));
                    throw new OperationCanceledException();
                }

                // See if anything's changed
                var responder = response.InteractionResponse.User.Id == user.Id ? user : user2;
                if (responder == user && !user1Confirmed)
                {
                    user1Confirmed = true;
                    shouldEditEmbed = true;
                }
                else if (responder == user2 && !user2Confirmed)
                {
                    user2Confirmed = true;
                    shouldEditEmbed = true;
                }
            }
            while (!user1Confirmed || !user2Confirmed);

            // confirmed!!
            embed.Description = DialogueDict.Get("PLUSHIE_TRADE_CONFIRM") + "\n\nTrade Was Confirmed By Both Users! ☺";
            response.BotMessage.ModifyAsync(a =>
            {
                a.Embed = embed.Build();
                a.Components = response.BotMessage.Components.ToBuilder().WithAllDisabled(true).Build();
            }).GetAwaiter();

            var result = await repo.AttemptTradeUserPlushiesAsync(connection, userPlushie1.UserPlushieId.ToString(), userPlushie2.UserPlushieId.ToString(),
                DateTime.Now);

            switch(result.Error)
            {
                case ModelRepository.TradePlushiesError.OneOrBothPlushiesMissing:
                    throw new CommandException(DialogueDict.Get("PLUSHIE_TRADE_MISSING"));
                case ModelRepository.TradePlushiesError.UnknownError:
                    throw new CommandException(DialogueDict.Get("EXCEPTION"));
                case ModelRepository.TradePlushiesError.None:
                    break;
            }

            embed = GetEmbed()
                .WithTitle($"Trade Complete!!")
                .WithDescription(DialogueDict.Get("PLUSHIE_TRADE_COMPLETE"));
            await response.InteractionResponse.FollowupAsync(embed: embed.Build());
        }



        protected override Task InteractionCreated(SocketInteraction interaction)
        {
            if (currentPrompt == null
                || currentPrompt.IsEntered
                || !(interaction is SocketMessageComponent mInteraction)
                || mInteraction.Message.Id != currentPrompt.BotMessage.Id)
                return Task.CompletedTask;

            mInteraction.DeferAsync().GetAwaiter();

            bool UserCanInteract(ulong id)
            {
                if (id == user.Id)
                    return currentPhase != Phase.U2ChooseCard || mInteraction.Data.CustomId.Equals("NO");
                else if (id == (user2?.Id ?? 0))
                {
                    return currentPhase == Phase.U2ChooseCard
                        || currentPhase == Phase.ConfirmOrCancel;
                }
                else
                    return false;
            }


            if (!UserCanInteract(interaction.User.Id))
            {
                var mention = user.Mention;
                if (currentPhase == Phase.U2ChooseCard)
                    mention = user2.Mention;
                var errorEmbed = EmbedHelper.GetEventErrorEmbed(mInteraction.User, $"Only {mention} can interact with that.", client);
                mInteraction.RespondAsync(embed: errorEmbed.Build(), ephemeral: true).GetAwaiter();
                return Task.CompletedTask;
            }

            currentPrompt.InteractionResponse = mInteraction;
            currentPrompt.IsEntered = true;
            return Task.CompletedTask;
        }
    }
}
