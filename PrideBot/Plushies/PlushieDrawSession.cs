using Discord;
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
    public class PlushieDrawSession : Session
    {
        protected ModelRepository repo;
        private PlushieImageService imageService;
        private IDiscordInteraction interaction;
        public bool IsRegistration;

        public PlushieDrawSession(IMessageChannel channel, SocketUser user, IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, PlushieImageService imageService, IDiscordInteraction interaction, bool isRegistration = false) : base(channel, user, config, client, timeout, originmessage)
        {
            this.repo = repo;
            this.imageService = imageService;
            this.interaction = interaction;
            IsRegistration = isRegistration;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var userId = user.Id.ToString();
            var day = GameHelper.IsEventOccuring(config) ? GameHelper.GetEventDay() : 0;
            if (!(await repo.CanUserDrawPlushieAsync(connection, userId, day)))
            {
                var key = day == 0 ? "PLUSHIE_DRAWN_ALREADY_PREREG" : "PLUSHIE_DRAWN_ALREADY";
                throw new CommandException(DialogueDict.Get(key));
            }

            var canReceivePlushie = await repo.CanUserReceivePlushieAsync(connection, userId);
            if (!canReceivePlushie)
                throw new CommandException(DialogueDict.Get("PLUSHIE_CANT_RECEIVE"));

            await repo.UpdatePlushieChoicesForUserAsync(connection, userId, day);
            var choices = await repo.GetPlushieChoicesForuserAsync(connection, userId, day);

            var imageFile = await imageService.WritePlushieCollectionImageAsync(choices);
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Want A Free Plushie?")
                .WithDescription(DialogueDict.Get((day == 0 ? "PLUSHIE_DRAW_DESCRIPTION_PREREG" : "PLUSHIE_DRAW_DESCRIPTION")))
                .WithAttachedImageUrl(imageFile);

            foreach (var choice in choices)
            {
                embed.AddField($"{choice.Name} ({choice.CharacterName})", choice.DecriptionUponUse());
            }

            //var choiceField = new EmbedFieldBuilder()
            //    .WithName("Your Choices Today:");
            //var choiceStr = "";
            //foreach (var choice in choices)
            //{
            //    choiceStr += $"\n**{choice.Name} ({choice.CharacterName}):**" +
            //        $"\n{choice.Description}";
            //}
            //choiceField.WithValue(choiceStr);
            //embed.WithFields(choiceField);

            var selectMenuOptions = choices
                .Select(a => new SelectMenuOptionBuilder(a.CharacterName, a.UserPlushieChoiceId.ToString(), a.Name))
                .ToList();

            var selectionComponents = new ComponentBuilder()
                .WithSelectMenu("DRAW_PLUSHIE", selectMenuOptions, "Which plushie would you like?")
                .WithButton("Nevermind, not now", "NO");

            var response = await SendAndAwaitNonTextResponseAsync(user.Mention, embed: embed, components: selectionComponents, interaction: interaction, file: imageFile);
            var selectionMessage = response.BotMessage;

            while(true)
            {
                if (response.IsNo)
                {
                    embed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("Nevermind?")
                        .WithDescription(DialogueDict.Get("PLUSHIE_DRAW_CANCEL"));
                    await response.InteractionResponse.FollowupAsync(embed: embed.Build());
                }
                else
                {
                    var choiceId = int.Parse(response.InteractionResponse.Data.Values.FirstOrDefault());
                    var choice = choices.FirstOrDefault(a => a.UserPlushieChoiceId == choiceId);

                    embed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("Use This Plushie?")
                        .WithDescription(DialogueDict.Get("PLUSHIE_CONFIRM_DRAW", choice.CharacterName));
                    var yesNoComponents = new ComponentBuilder()
                        .WithButton("Yeah!", "YES", ButtonStyle.Success, new Emoji("👍"))
                        .WithButton("Actually Hold On", "NO", ButtonStyle.Secondary, new Emoji("❌"));

                    response = await SendAndAwaitNonTextResponseAsync(user.Mention, embed: embed, components: yesNoComponents, interaction: interaction);
                    if (response.IsNo)
                    {
                        response.BotMessage.DeleteAsync().GetAwaiter();
                        selectionMessage.ModifyAsync(a => a.Components = selectionComponents.Build()).GetAwaiter();
                        currentPrompt = new Prompt(selectionMessage, false, null, true);
                        response = await AwaitCurrentResponseAsync();
                        continue;
                    }

                    var newDay = GameHelper.IsEventOccuring(config) ? GameHelper.GetEventDay() : 0; // in case user waits around forever idk
                    var result = await repo.AttemptAddUserPlushieAsync(connection, userId, null, userId, choice.PlushieId, choice.CharacterId, newDay, choice.Rotation, PlushieTransaction.Drawn, choiceId);

                    result.CheckErrors();

                    var resultFile = await imageService.WritePlushieImageAsync(choice);
                    var resultEmbed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("BOOM! Plushie!")
                        .WithDescription(DialogueDict.Get("PLUSHIE_DRAWN"))
                        .WithAttachedThumbnailUrl(resultFile);
                    if (IsRegistration && !GameHelper.IsEventOccuring(config))
                        resultEmbed.Description += "\n\n" + DialogueDict.Get("GETPLUSHIE_PREREG", config.GetDefaultPrefix());
                    else if (IsRegistration)
                        resultEmbed.Description += "\n\n" + DialogueDict.Get("GETPLUSHIE_REG", config.GetDefaultPrefix());
                    else if (interaction == null)
                        resultEmbed.Description += "\n\n" + DialogueDict.Get("GETPLUSHIE_PROMPT", config.GetDefaultPrefix());
                    else
                        resultEmbed.Description += "\n\n" + DialogueDict.Get("GETPLUSHIE_SCROLLUP");
                    await response.InteractionResponse.FollowupWithFileAsync(resultFile.Stream, resultFile.FileName, embed: resultEmbed.Build());
                }
                break;
            }
        }
    }
}
