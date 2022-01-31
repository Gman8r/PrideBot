using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Game;
using PrideBot.Models;
using PrideBot.Quizzes;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Plushies
{
    public partial class PlushieEffectSession : Session
    {
        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly PlushieImageService imageService;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly SqlConnection connection;
        readonly IGuildUser user;
        readonly UserPlushie userPlushie;
        readonly IMessageChannel channel;

        IDiscordInteraction interaction;

        public PlushieEffectSession(IMessageChannel channel, SocketGuildUser user, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, ModelRepository repo, PlushieImageService imageService, ScoringService scoringService, SqlConnection connection, UserPlushie userPlushie, IDiscordInteraction interaction = null) : base(channel, user, config, client, timeout, originmessage)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
            this.scoringService = scoringService;
            this.connection = connection;
            this.user = user;
            this.userPlushie = userPlushie;
            this.channel = channel;
            this.interaction = interaction;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Use This Plushie?")
                .WithDescription(DialogueDict.Get("PLUSHIE_CONFIRM_ACTIVATE", userPlushie.CharacterName, userPlushie.Name));
            var yesNoComponents = new ComponentBuilder()
                .WithButton("Yeah!", "YES", ButtonStyle.Success, new Emoji("👍"))
                .WithButton("Hmm, Not Right Now", "NO", ButtonStyle.Secondary, new Emoji("❌"));
            var response = await SendAndAwaitNonTextResponseAsync(embed: embed, components: yesNoComponents, interaction: interaction);
            if (response.IsNo)
            {
                MarkCancelled("Laters then 🧸");
                throw new OperationCanceledException();
            }
            if (!userPlushie.StacksSelf)
            {
                var alreadyActive = (await repo.GetInEffectUserPlushiesForUserAsync(connection, user.Id.ToString(), DateTime.Now))
                    .Where(a => a.PlushieId.Equals(userPlushie.PlushieId));
                if (alreadyActive.Any())
                {
                    embed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("Hey Hold Up! ⏱")
                        .WithDescription(DialogueDict.Get("PLUSHIE_CANT_STACK"));
                    response = await SendAndAwaitNonTextResponseAsync(embed: embed, components: yesNoComponents, interaction: interaction);
                    if (response.IsNo)
                    {
                        MarkCancelled("Laters then 🧸");
                        throw new OperationCanceledException();
                    }
                }
            }
            interaction = response.InteractionResponse;

            await ActivatePlushieAsync(userPlushie);
        }

        async Task ActivatePlushieAsync(UserPlushie userPlushie)
        {
            try
            {
                switch (userPlushie.PlushieId)
                {
                    case "ADVANCED_MATHEMATICS":
                        await AdvancedMathematicsAsync(userPlushie);
                        break;
                    case "COPY_CAT":
                        await CopyCatAsync(userPlushie);
                        break;
                    case "MYSTERY_MEDICINE":
                        await MysteryMedicineAsync(userPlushie);
                        break;
                    case "CLEARANCE_SALE":
                        await ClearanceSaleAsync(userPlushie);
                        break;
                    case "QUIZ_DOUBLE_DARE":
                        await QuizDoubleDareAsync(userPlushie);
                        break;
                    case "FACTORY_RESET":
                        await RandomResetAsync(userPlushie);
                        break;
                    case "UNDO":
                    case "1_UP":
                        await UndoCooldownsAsync(userPlushie);
                        break;
                    default:
                        await ActivatePlushieDefaultAsync(userPlushie);
                        break;
                }
            }
            catch (CommandException e)
            {
                var embed = EmbedHelper.GetEventErrorEmbed(user, e.ParsedMessage, client);
                await interaction.FollowupAsync(embed: embed.Build());
            }
        }

        async Task ActivatePlushieDefaultAsync(UserPlushie userPlushie)
        {
            await repo.ActivateUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now);
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Activated!!")
                .WithDescription(userPlushie.DurationHours > 0
                ? DialogueDict.Get("PLUSHIE_ACTIVATED_DURATION", userPlushie.CharacterName, userPlushie.DurationHours)
                : DialogueDict.Get("PLUSHIE_ACTIVATED_USES", userPlushie.CharacterName));
            if (interaction == null)
                await channel.SendMessageAsync(embed: embed.Build());
            else
                await interaction.FollowupAsync(embed: embed.Build());
        }

        async Task UndoCooldownsAsync(UserPlushie userPlushie)
        {
            await repo.NullifyAchievementCoooldowns(connection, new DateTime(2020, 1, 1));
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Let's Rewind! 🔙")
                .WithDescription(DialogueDict.Get("PLUSHIE_1_UP"));
            var msg = await interaction.FollowupAsync(embed: embed.Build());

            // deplete plushie
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
        }

        async Task QuizDoubleDareAsync(UserPlushie userPlushie)
        {
            var activeSession = GetActiveSession(user.Id);
            if (activeSession == null || !(activeSession is QuizSession qSession) || !qSession.QuizStarted)
            {
                await ActivatePlushieDefaultAsync(userPlushie);
                return;
            }

            // user is already in a quiz
            throw new CommandException("This should not be seen, so congratulations I guess!!");
        }

        async Task ClearanceSaleAsync(UserPlushie userPlushie)
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
                .WithTitle("BAM! 🎆 Goodbye, All! 👋")
                .WithDescription(DialogueDict.Get("PLUSHIE_CLEARANCE_SELL", otherPlushies.Count(), pointTotal));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // grant score for achievement
            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_CLEARANCE", client.CurrentUser, DateTime.Now,
                msg, pointTotal, msg.GetJumpUrl(), appliedPlushie: userPlushie);

            // deplete plushie
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
            // and the rest
            foreach (var otherPlushie in otherPlushies)
            {
                await repo.DepleteUserPlushieAsync(connection, otherPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
            }
        }

        async Task MysteryMedicineAsync(UserPlushie userPlushie)
        {
            // get random value
            var pointTotal = await repo.GetMysteryMedicineMultAsync(connection);
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("What A Strange Taste! ⁉")
                .WithDescription(DialogueDict.Get("PLUSHIE_MEDICINE_GET", pointTotal));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // grant score for achievement
            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_MEDICINE", client.CurrentUser, DateTime.Now,
                msg, pointTotal, msg.GetJumpUrl(), appliedPlushie: userPlushie);

            // deplete plushie
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
        }

        async Task CopyCatAsync(UserPlushie userPlushie)
        {
            var lastUsed = (await repo.GetAllUserPlushiesForUserAsync(connection, user.Id.ToString()))
                .Where(a => (a.Fate == PlushieTransaction.Using || a.Fate == PlushieTransaction.Done)
                    && a.RemovedTimestamp != null
                    && a.Context.Equals("CARD_MENU")
                    && !a.PlushieId.Equals("COPY_CAT")) // no self-copies
                .OrderBy(a => a.RemovedTimestamp)
                .LastOrDefault();
            if (lastUsed == null)
                throw new CommandException(DialogueDict.Get("PLUSHIE_COPYCAT_NONE"));

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Copying! 😽")
                .WithDescription(DialogueDict.Get("PLUSHIE_COPYCAT", lastUsed.CharacterName));
            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // deplete copy cat first
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());

            // add a new user plushie from it
            var newUserPlushie = await repo.ForceAddPlushieAsync(connection, user.Id.ToString(), lastUsed.CharacterId, lastUsed.PlushieId, lastUsed.Rotation);

            // And activate
            await ActivatePlushieAsync(newUserPlushie);

        }

        async Task RandomResetAsync(UserPlushie userPlushie)
        {
            // Get random character id for a plushie, using the algorithm in the db
            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, user.Id.ToString());
            if (!userPlushies.Any(a => !a.UserPlushieId.Equals(userPlushie.UserPlushieId)))
                throw new CommandException(DialogueDict.Get("PLUSHIE_RESET_NONE"));
            var seletedCharacterId = await repo.GetRandomPlushieCharacterAsync(connection, user.Id.ToString());
            var selectedPlushie = await repo.GetPlushieFromCharaterAsync(connection, seletedCharacterId);
            var selectedCharacter = await repo.GetCharacterAsync(connection, seletedCharacterId);

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Resetting! ❓")
                .WithDescription(DialogueDict.Get("PLUSHIE_RESET_START", selectedCharacter.Name));
            var msg = await channel.SendMessageAsync(embed: embed.Build());
            using var typing = channel.EnterTypingState();

            // deplete this card
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
            // for all cards that Aren't this one
            foreach (var ownedPlushie in userPlushies.Where(a => !a.UserPlushieId.Equals(userPlushie.UserPlushieId)))
            {
                // deplete card
                await repo.DepleteUserPlushieAsync(connection, ownedPlushie.UserPlushieId, DateTime.Now, true, PlushieEffectContext.Message, msg.Id.ToString());
                // add our random card
                await repo.ForceAddPlushieAsync(connection, user.Id.ToString(), selectedCharacter.CharacterId, selectedPlushie.PlushieId, ownedPlushie.Rotation);
            }

            embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Reset! 🙈")
                .WithDescription(DialogueDict.Get("PLUSHIE_RESET_DONE", selectedCharacter.Name));
            await channel.SendMessageAsync(embed: embed.Build());
        }

        async Task AdvancedMathematicsAsync(UserPlushie userPlushie)
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
