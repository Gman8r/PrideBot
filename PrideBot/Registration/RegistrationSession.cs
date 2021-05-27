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
using PrideBot.Game;

namespace PrideBot.Registration
{
    public class RegistrationSession : DMSession
    {
        protected static IEmote DeleteEmote => new Emoji("🗑");

        readonly ShipImageGenerator shipImageGenerator;
        readonly ModelRepository repo;
        readonly ScoringService scoringService;
        UserRegisteredCache userRegs;

        bool userHasRegistered;
        User dbUser;
        UserShipCollection dbUserShips;

        public RegistrationSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, ScoringService scoringService, UserRegisteredCache userRegs) : base(channel, user, config, client, timeout, originmessage)
        {
            this.shipImageGenerator = shipImageGenerator;
            this.repo = repo;
            this.scoringService = scoringService;
            this.userRegs = userRegs;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {

            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            dbUserShips = new UserShipCollection();
            dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            if (dbUser != null)
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
            userHasRegistered = dbUser.ShipsSelected;


            var embed = GetEmbed()
                .WithTitle(userHasRegistered ? "Edit Your Registration!" : "Registration Time!")
                .WithDescription(userHasRegistered
                ? DialogueDict.Get("REGISTRATION_EDIT", user.Queen(client))
                : DialogueDict.Get("REGISTRATION_WELCOME", user.Queen(client), config.GetDefaultPrefix()));

            //embed.ImageUrl = config.GetRelativeHostPathWeb(await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser));

            var firstResponse = await SendAndAwaitYesNoResponseAsync(embed: embed);
            if (!firstResponse.IsYes)
            {
                await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                return;
            }

            // Main loop for reg finalization
            while(true)
            {
                embed = GetEmbed()
                    .WithDescription("");
                for (int i = 0; i < 3; i++)
                {
                    embed.Description = await SetUpShip(connection, (UserShipTier)i, embed);
                }

                embed = GetEmbed()
                    .WithTitle("Choose a Background")
                    .WithDescription(embed.Description
                    + "\n\n" + DialogueDict.Get("REGISTRATION_CUSTOMIZE_BG", config.GetDefaultPrefix())
                    + "\n\n" + DialogueDict.Get("REGISTRATION_CHOOSE_BG"));

                while (true)
                {
                    var emotes = Enumerable.Range(1, Directory.GetFiles("Assets/Backgrounds").Length)
                        .Select(a => new Emoji(EmoteHelper.NumberEmotes[a]) as IEmote)
                        .ToList();
                    emotes.Insert(0, YesEmote);
                    embed.ImageUrl = config.GetRelativeHostPathWeb(await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser));
                    var bgResponse = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: emotes);

                    if (bgResponse.IsYes)
                        break;
                    dbUser.CardBackground = emotes.FindIndex(a => a.ToString().Equals(bgResponse.EmoteResponse.ToString()));
                    embed = (await GetEmbedWithShipsAsync(dbUser, dbUserShips))
                        .WithTitle("Background Config")
                        .WithDescription(DialogueDict.Get("REGISTRATION_BG_CHANGED", user.Queen(client)));
                    await repo.UpdateUserAsync(connection, dbUser);
                    await channel.SendMessageAsync(embed: embed.Build());

                    embed = GetEmbed()
                        .WithTitle("Choose a Background")
                        .WithDescription(DialogueDict.Get("REGISTRATION_CHOOSE_BG"));
                }

                embed = (await GetEmbedWithShipsAsync(dbUser, dbUserShips))
                    .WithTitle("Confirm Please!")
                    .WithDescription(DialogueDict.Get(dbUser.ShipsSelected ? "REGISTRATION_CONFIRM_EDIT" : "REGISTRATION_CONFIRM"));
                var confirmChoices = new List<IEmote>() { YesEmote, new Emoji("↩") };
                if (!dbUser.ShipsSelected)
                    confirmChoices.Add(NoEmote);
                var result = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: confirmChoices);

                if (result.IsYes)
                    break;
                else if (result.IsNo)
                {
                    await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                    return;
                }
            }

            var key = "REGISTRATION_" + (userHasRegistered ? "EDITED" : "COMPLETE") + (GameHelper.IsEventOccuring(config) ? "" : "_PREREG");
            embed = GetEmbed()
                .WithTitle("Setup Complete!")
                .WithDescription(DialogueDict.Get(key, config.GetDefaultPrefix()));
            await channel.SendMessageAsync(embed: embed.Build());
            if (!userHasRegistered)
            {
                dbUser.ShipsSelected = true;
                await repo.UpdateUserAsync(connection, dbUser);
                userRegs[user.Id.ToString()] = true;

                var achievementId = GameHelper.IsEventOccuring(config) ? "REGISTER" : "PREREGISTER";
                var achievement = await repo.GetAchievementAsync(connection, achievementId);
                await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, client.CurrentUser);

                // Reward points from before user registered if needed
                var storedPoints = await repo.GetUserNonRegPoints(connection, dbUser.UserId);
                if (storedPoints > 0)
                {
                    var pointsAchievement = await repo.GetAchievementAsync(connection, "STORED");
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, pointsAchievement, client.CurrentUser, overridePoints: storedPoints);
                }

                // Give registered role
                var gyn = client.GetGyn(config);
                var registeredRole = gyn.GetRoleFromConfig(config, "registeredrole");
                var guildUser = gyn.GetUser(user.Id);
                if (guildUser != null)
                {
                    try
                    {
                        await guildUser.AddRoleAsync(registeredRole);
                    }
                    catch (Exception e)
                    {
                        var modEmbed = EmbedHelper.GetEventErrorEmbed(null, $"OH NO! Sages I'm totes sorry to bug you all but I need my role to be higher so I can give people the registration role! And then give the {registeredRole.Mention} role to my bestie {guildUser.Mention}, pretty please?", client, showUser: false);
                        var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                        await modChannel.SendMessageAsync(embed: modEmbed.Build());
                    }
                }
            }
            
        }

        // Returns prefix for next message
        async Task<string> SetUpShip(SqlConnection connection, UserShipTier tier, EmbedBuilder embed)
        {
            var isNewShip = !dbUserShips.Has(tier);

            var title = $"{tier} Pair Setup";
            var tierKey = userHasRegistered ? "EDIT" : tier.ToString().ToUpper();
            var pointPercent = GameHelper.GetPointPercent(await repo.GetScoreRatioForShipTierAsync(connection, tier));
            embed.Description += "\n\n" + DialogueDict.Get($"REGISTRATION_ENTER_{tierKey}", tier.ToString().ToLower(), pointPercent);

            var enterInstructions = DialogueDict.Get("REGISTRATION_SHIP_FORMAT");
            if (isNewShip && tier != UserShipTier.Primary)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_SKIP_SHIP", SkipEmote.ToString(), tier.ToString().ToLower());
            else if (!isNewShip)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_KEEP_SHIP", SkipEmote.ToString());
            embed.Description += "\n\n" + enterInstructions;

            // Determine what bypasses are possible
            var canSkip = tier != UserShipTier.Primary || userHasRegistered || !isNewShip;

            // Now register the ship
            var shipValidated = false;
            Prompt response;
            embed.Title = title;
            var skipped = false;
            UserShip selectedUserShip = null;
            while (!shipValidated)
            {
                embed.ImageUrl = await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier);
                response = await SendAndAwaitResponseAsync(embed: embed, canSkip: canSkip);
                if (response.IsSkipped)
                {
                    skipped = true;
                    if (isNewShip)
                        return "";
                    else
                        break;
                }

                var inputResult = await ProcessPairingInputAsync(connection, response.MessageResponse.Content, tier);
                if (!inputResult.IsSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(inputResult.ErrorMessage))
                    {
                        var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, $"{inputResult.ErrorMessage}",
                            client, showUser: false);
                        await channel.SendMessageAsync(embed: errorEmbed.Build());
                    }
                    embed.Description = $"{DialogueDict.Get("SESSION_TRY_AGAIN")} {enterInstructions}";
                }
                else
                {
                    shipValidated = true;
                }
            }
            selectedUserShip ??= dbUserShips.Get(tier);

            if (!skipped)
            {
                var conirmationEmbed = GetEmbed()
                    .WithTitle(DialogueDict.Get("REGISTRATION_SHIP_ENTERED"))
                    .WithDescription(DialogueDict.Get("REGISTRATION_SHIP_REVIEW", selectedUserShip.GetDisplayName(), tier.ToString().ToLower()));
                await channel.SendMessageAsync(embed: conirmationEmbed.Build());
            }

            embed = embed.WithDescription(DialogueDict.Get("REGISTRATION_HEART_PROMPT")
                    + "\n\n" + DialogueDict.Get("REGISTRATION_HEART_CHOOSE", selectedUserShip.Character1First, SkipEmote))
                .WithImageUrl(await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier, highlightHeart: 1))
                .WithTitle($"{tier} Pair Heart Setup");
            var heartEmotes = client.GetGuild(796585563166736394).Emotes.Where(a => a.Name.StartsWith("shipheart"))
                .Select(a => (IEmote)a)
                .ToList();
            response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
            if (!response.IsSkipped)
            {
                selectedUserShip.Heart1 = ((Emote)response.EmoteResponse).Name;
                await repo.UpdateUserShipAsync(connection, selectedUserShip);
                embed = embed
                    .WithDescription(DialogueDict.Get("REGISTRATION_HEART_CHOOSE", selectedUserShip.Character2First, SkipEmote))
                    .WithImageUrl(await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier, highlightHeart: 2));
                response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
                if (!response.IsSkipped)
                {
                    selectedUserShip.Heart2 = ((Emote)response.EmoteResponse).Name;
                    await repo.UpdateUserShipAsync(connection, selectedUserShip);
                }

            }

            return DialogueDict.Get("REGISTRATION_FINISH_SHIP", user.Queen(client));
        }

        async Task<Result> ProcessPairingInputAsync(SqlConnection connection, string shipStr, UserShipTier tier)
        {
            if (shipStr.ToLower().Contains("drop table"))
                return Result.Error("HAHAHAHA THAT'S SO FUNNY I HOPE YOU KNOW HOW FUNNY YOU ARE !!!");
            using var typingState = channel.EnterTypingState();
            var dbCharacters = await repo.GetAllCharactersAsync(connection);
            var parseResult = await ParseShipAsync(connection, shipStr, dbCharacters);
            if (!parseResult.IsSuccess)
                return parseResult.ToResult();
            var ship = parseResult.Value;

            // Don't recreate ship if they just inputed the same one (to preserve ship hearts)
            if (((dbUserShips.Get(tier)?.ShipId) ?? "").Equals(ship.ShipId))
                return Result.Success();

            var validateResult = await ValidateShipAsync(connection, ship, dbCharacters);
            if (!validateResult.IsSuccess)
                return validateResult;

            typingState.Dispose();
            var duplicateShip = dbUserShips.FirstOrDefault(a => a.Tier != (int)tier && a.ShipId.Equals(ship.ShipId));
            if (duplicateShip != null)
            {
                var result = await HandleDuplicateShipAsync(connection, duplicateShip, tier);
                if (!result.IsSuccess)
                    return result.ToResult();
                if (result.Value)
                    return Result.Success();
            }
            await repo.CreateOrReplaceUserShip(connection, dbUser.UserId, tier, ship.ShipId);
            dbUserShips = await repo.GetUserShipsAsync(connection, dbUser.UserId);
            return Result.Success();
        }

        async Task<ValueResult<Ship>> ParseShipAsync(SqlConnection connection, string shipStr, IEnumerable<Character> dbCharacters)
        {
            using var typingState = channel.EnterTypingState();
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_FORMAT"));

            var char1 = FindMatch(split[0], dbCharacters);
            var char2 = FindMatch(split[1], dbCharacters);

            if (char1 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[0]));
            if (char2 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[1]));

            var shipKey = await repo.GetOrCreateShipAsync(connection, char1.CharacterId, char2.CharacterId);
            return ValueResult<Ship>.Success(await repo.GetShipAsync(connection, shipKey));
        }

        async Task<Result> ValidateShipAsync(SqlConnection connection, Ship ship, IEnumerable<Character> dbCharacters)
        {
            var char1 = dbCharacters
                .FirstOrDefault(a => a.CharacterId == ship.CharacterId1);
            var char2 = dbCharacters
                .FirstOrDefault(a => a.CharacterId == ship.CharacterId2);

            if (ship.CharacterId1.Equals("YURIKO") || ship.CharacterId2.Equals("YURIKO"))
            {
                if (ship.CharacterId1.Equals("JOON") || ship.CharacterId2.Equals("JOON"))
                    return Result.Error("DAMN 💦💦 ok she's Got It but I still can't, RIIIPP.");
                else
                    return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_YURIKO"));
            }
            if (char1.CharacterId.Equals(char2.CharacterId) && !char1.CharacterId.Equals("TSUCHINOKO"))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_SELF"));
            if (ship.IsBlacklisted)
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));

            var categories = new string[] { char1.Category, char2.Category };
            if (categories.Contains("AMBIGUOUS"))
            {
                var compatibleFields = new string[] { "AMBIGUOUS", "ADULT", "CHILD" };
                if (!compatibleFields.Contains(char1.Category) || !compatibleFields.Contains(char2.Category))
                    return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));
            }
            else if (!char1.Category.Equals(char2.Category))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));

            if (!string.IsNullOrWhiteSpace(char1.Family) && char1.Family.Equals(char2.Family))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));



            return Result.Success();
        }
        async Task<ValueResult<bool>> HandleDuplicateShipAsync(SqlConnection connection, UserShip duplicateShip, UserShipTier currentTier)
        {
            // Handle tier duplicates and swapping
            if (duplicateShip.Tier == (int)UserShipTier.Primary && !dbUserShips.Has(currentTier))
                return ValueResult<bool>.Error(DialogueDict.Get("REGISTRATION_ERROR_PRIMARY_DUPE"));
            var embed = GetEmbed()
                .WithTitle("Pairing Conflict")
                .WithDescription(DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE",
                ((UserShipTier)duplicateShip.Tier).ToString().ToLower(), duplicateShip.GetDisplayName(), currentTier.ToString().ToLower()));
            if (dbUserShips.Has(currentTier))
            {
                var currentShip = dbUserShips.Get(currentTier);
                embed.Description += " " + DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE_SWAP",
                currentTier.ToString().ToLower(), currentShip.GetDisplayName());
            }
            var response = await SendAndAwaitYesNoResponseAsync(embed: embed);
            if (response.IsYes)
            {
                await repo.SwapShipTiersAsync(connection, dbUser.UserId, (UserShipTier)duplicateShip.Tier, currentTier);
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
                return ValueResult<bool>.Success(true);
            }
            else
                return ValueResult<bool>.Error("");
        }

        Character FindMatch(string inputName, IEnumerable<Character> characters)
        {
            var words = inputName
                .ToUpper()
                .Split()
                .Where(a => a.Any())
                .Select(a => a.Replace("'", ""))
                .ToArray();
            return characters
                .FirstOrDefault(a => !words.Except(a.Name.Replace("'", "").ToUpper().Split()).Any());   // All words in input name appear in character name
        }

        async Task<EmbedBuilder> GetEmbedWithShipsAsync(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0)
        => GetEmbed()
            .WithImageUrl(await GenerateShipImage(dbUser, dbShips, highlightTier, highlightHeart));

        public async Task<string> GenerateShipImage(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0)
        {
            var image = await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips, highlightTier, highlightHeart);
            return config.GetRelativeHostPathWeb(image);
        }

        protected override string GetTimeoutMessage() => base.GetTimeoutMessage() + "\n\n" + DialogueDict.Get("SESSION_TIMEOUT_SAVED");
    }
}
