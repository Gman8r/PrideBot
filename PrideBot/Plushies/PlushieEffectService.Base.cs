﻿using Discord;
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
    public partial class PlushieEffectService
    {
        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly PlushieImageService imageService;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;

        public PlushieEffectService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client, ScoringService scoringService)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
            this.scoringService = scoringService;
        }

        public async Task ActivatePlushie(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction = null)
        {
            EmbedBuilder embed;
            switch(userPlushie.PlushieId)
            {
                case "ADVANCED_MATHEMATICS":
                    await AdvancedMathematicsAsync(connection, user, userPlushie, channel, interaction);
                    break;
                case "COPY_CAT":
                    await CopyCatAsync(connection, user, userPlushie, channel, interaction);
                    break;
                case "MYSTERY_MEDICINE":
                    await MysteryMedicineAsync(connection, user, userPlushie, channel, interaction);
                    break;
                case "CLEARANCE_SALE":
                    await ClearanceSaleAsync(connection, user, userPlushie, channel, interaction);
                    break;
                case "RANDOM_RESET":
                    await RandomResetAsync(connection, user, userPlushie, channel, interaction);
                    break;
                default:
                    await repo.ActivateUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now);
                    embed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("Activated!!")
                        .WithDescription(userPlushie.DurationHours > 0
                        ? DialogueDict.Get("PLUSHIE_ACTIVATED_DURATION", userPlushie.CharacterName, userPlushie.DurationHours)
                        : DialogueDict.Get("PLUSHIE_ACTIVATED_USES", userPlushie.CharacterName));
                    if (interaction == null)
                        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
                    else
                        await interaction.FollowupAsync(user.Mention, embed: embed.Build());
                    break;
            }
        }
        async Task ClearanceSaleAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            // get other plushies
            var otherPlushies = (await repo.GetOwnedUserPlushiesForUserAsync(connection, user.Id.ToString()))
                .Where(a => !a.UserPlushieId.Equals(userPlushie.UserPlushieId));

            if (!otherPlushies.Any())
                throw new CommandException(DialogueDict.Get("PLUSHIE_CLEARANCE_NONE"));

            // get point amount and user cards
            var pointsPerCard = await repo.GetClearanceSaleCardValueAsync(connection);
            var pointTotal = pointsPerCard * otherPlushies.Count();
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("🎆 BAM! Goodbye, All!")
                .WithDescription(DialogueDict.Get("PLUSHIE_CLEARANCE_SELL", otherPlushies.Count(), pointTotal));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // grant score for achievement
            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_MEDICINE", client.CurrentUser, DateTime.Now,
                msg, pointTotal, msg.GetJumpUrl(), appliedPlushie: userPlushie);

            // deplete plushie
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
        }

        async Task MysteryMedicineAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            // get random value
            var pointTotal = await repo.GetMysteryMedicineMultAsync(connection);
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("⁉ What A Strange Taste!")
                .WithDescription(DialogueDict.Get("PLUSHIE_MEDICINE_GET", pointTotal));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // grant score for achievement
            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_MEDICINE", client.CurrentUser, DateTime.Now,
                msg, pointTotal, msg.GetJumpUrl(), appliedPlushie: userPlushie);

            // deplete plushie
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
        }

        async Task CopyCatAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            var lastUsed = (await repo.GetAllUserPlushiesForUserAsync(connection, user.Id.ToString()))
                .Where(a => (a.Fate == PlushieTransaction.Using || a.Fate == PlushieTransaction.Done)
                    && a.RemovedTimestamp != null
                    && !a.PlushieId.Equals("COPY_CAT")) // no self-copies
                .OrderBy(a => a.RemovedTimestamp)
                .LastOrDefault();
            if (lastUsed == null)
                throw new CommandException(DialogueDict.Get("PLUSHIE_COPYCAT_NONE"));

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("😽 Copying!")
                .WithDescription(DialogueDict.Get("PLUSHIE_COPYCAT", lastUsed.CharacterName));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // deplete copy cat first
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());

            // add a new user plushie from it
            var newUserPlushie = await repo.ForceAddPlushieAsync(connection, user.Id.ToString(), lastUsed.CharacterId, lastUsed.PlushieId, lastUsed.Rotation);
   
            // And activate
            await ActivatePlushie(connection, user, newUserPlushie, channel, interaction);

        }

        async Task RandomResetAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            // Get random character id for a plushie, using the algorithm in the db
            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, user.Id.ToString());
            if (!userPlushies.Any(a => a.UserPlushieId.Equals(a.UserPlushieId.Equals(userPlushie.UserPlushieId))))
                throw new CommandException(DialogueDict.Get("PLUSHIE_RESET_NONE"));
            var seletedCharacterId = await repo.GetRandomPlushieCharacterAsync(connection, user.Id.ToString());
            var selectedPlushie = await repo.GetPlushieFromCharaterAsync(connection, seletedCharacterId);
            var selectedCharacter = await repo.GetCharacterAsync(connection, seletedCharacterId);

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("❓ Resetting!")
                .WithDescription(DialogueDict.Get("PLUSHIE_RESET_START", selectedCharacter.Name));
            var msg = await channel.SendMessageAsync(embed: embed.Build());
            using var typing = channel.EnterTypingState();

            // deplete this card
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
            // for all cards that Aren't this one
            foreach (var ownedPlushie in userPlushies.Where(a => a.UserPlushieId.Equals(a.UserPlushieId.Equals(userPlushie.UserPlushieId))))
            {
                // deplete card
                await repo.DepleteUserPlushieAsync(connection, ownedPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
                // add our random card
                await repo.ForceAddPlushieAsync(connection, user.Id.ToString(), selectedCharacter.CharacterId, selectedPlushie.PlushieId, ownedPlushie.Rotation);
            }

            embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("❗ Reset!")
                .WithDescription(DialogueDict.Get("PLUSHIE_RESET_DONE", selectedCharacter.Name));
            await channel.SendMessageAsync(embed: embed.Build());
        }

        async Task AdvancedMathematicsAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            var allShips = await repo.GetAllShipsAsync(connection);
            var userShips = await repo.GetUserShipsAsync(connection, user.Id.ToString());
            var supportedShips = allShips
                .Where(a => userShips.Any(aa => aa.ShipId == a.ShipId));

            var tutorial = "";
            var sum = supportedShips.Sum(a => (int)Math.Round(a.PointsEarned));
            if (supportedShips.Count() > 1)
                tutorial += string.Join("\n+ ", supportedShips.Select(a => $" **{(int)Math.Round(a.PointsEarned)}** from **{a.GetDisplayName()}**")) + "\n= ";
            else
                tutorial += $"Your only supported pairing (**{supportedShips.FirstOrDefault().GetDisplayName()}**) has ";

            var ones = sum % 10;
            var tens = ((sum - (sum % 10)) / 10) % 10;
            var hundredsPlus = (sum - (sum % 100)) / 100;
            if (hundredsPlus > 0)
                tutorial += hundredsPlus.ToString();
            tutorial += $"**{tens}{ones}** {EmoteHelper.SPEmote}.";

            var digitsSum = ones + tens;
            var pointMult = 5; // TODO db mult
            var pointsTotal = digitsSum * pointMult;
            tutorial += $"\n\n{ones} + {tens} = **{digitsSum}**";
            tutorial += "\n\n UHHHHH i'll figure out the mult later, let's just say x5. Also i think she should have different reactions depending on how high you get and for the low ones she basically makes fun of you lowkey.";

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Okay! Leeeeet's see!")
                .WithThumbnailUrl(client.CurrentUser.GetServerAvatarUrlOrDefault())
                .WithDescription(tutorial);
            var msg = await interaction.FollowupAsync(embed: embed.Build());

            var scoreResult = await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_MATH", client.CurrentUser, DateTime.Now, msg, pointsTotal, msg.GetJumpUrl(), appliedPlushie: userPlushie);
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
        }
    }
}
