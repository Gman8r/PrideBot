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

        public PlushieDrawSession(IMessageChannel channel, SocketUser user, IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, PlushieImageService imageService, IDiscordInteraction interaction) : base(channel, user, config, client, timeout, originmessage)
        {
            this.repo = repo;
            this.imageService = imageService;
            this.interaction = interaction;
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

            await repo.UpdatePlushieChoicesForUserAsync(connection, userId, day);
            var choices = await repo.GetPlushieChoicesForuserAsync(connection, userId, day);

            var imageFile = await imageService.WritePlushieCollectionImageAsync(choices);
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Want A Free Plushie?")
                .WithDescription(DialogueDict.Get((day == 0 ? "PLUSHIE_DRAW_DESCRIPTION_PREREG" : "PLUSHIE_DRAW_DESCRIPTION")))
                .WithAttachedImageUrl(imageFile);

            foreach (var choice in choices)
            {
                embed.AddField($"{choice.Name} ({choice.CharacterName})", choice.Description);
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

            var components = new ComponentBuilder()
                .WithSelectMenu("DRAW_PLUSHIE", selectMenuOptions, "Which plushie would you like?")
                .WithButton("Nevermind, not now", "NO");

            var response = await SendAndAwaitNonTextResponseAsync(user.Mention, embed: embed, components: components, interaction: interaction, file: imageFile);

            if (response.IsNo)
                await response.InteractionResponse.FollowupAsync(DialogueDict.Get("PLUSHIE_DRAW_CANCEL"));
            else
            {
                var choiceId = int.Parse(response.InteractionResponse.Data.Values.FirstOrDefault());
                var choice = choices.FirstOrDefault(a => a.UserPlushieChoiceId == choiceId);
                var newDay = GameHelper.IsEventOccuring(config) ? GameHelper.GetEventDay() : 0; // in case user waits around forever idk
                var result = await repo.AttemptAddUserPlushieAsync(connection, userId, null, userId, choice.PlushieId, choice.CharacterId, newDay, choice.Rotation, PlushieTransaction.Drawn, choiceId);

                result.CheckErrors();

                var resultFile = await imageService.WritePlushieImageAsync(choice);
                var resultEmbed = EmbedHelper.GetEventEmbed(user, config)
                    .WithTitle("BOOM! Plushie!")
                    .WithDescription(DialogueDict.Get("PLUSHIE_DRAWN"))
                    .WithAttachedThumbnailUrl(resultFile);
                await response.InteractionResponse.FollowupWithFileAsync(resultFile.Stream, resultFile.FileName, embed: resultEmbed.Build());
            }
        }
    }
}
