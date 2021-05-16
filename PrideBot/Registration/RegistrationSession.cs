using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Registration
{
    public class RegistrationSession : DMSession
    {
        protected static IEmote DeleteEmote => new Emoji("🗑");

        readonly ShipImageGenerator shipImageGenerator;
        readonly ModelRepository repo;
        Dictionary<string, string> dialogue;

        bool userHasRegistered;
        User dbUser;
        UserShipCollection dbUserShips;

        public RegistrationSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage) : base(channel, user, config, client, timeout, originmessage)
        {
            this.shipImageGenerator = shipImageGenerator;
            this.repo = repo;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {

            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

            dbUserShips = new UserShipCollection();
            dbUser = await repo.GetUserAsync(connection, user.Id.ToString());
            if (dbUser == null)
            {
                await repo.AddUserAsync(connection, new User() { UserId = user.Id.ToString() });
                dbUser = await repo.GetUserAsync(connection, user.Id.ToString());
            }
            else                
            {
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
            }
            userHasRegistered = dbUser.ShipsSelected;


            var embed = GetEmbed()
                .WithTitle(userHasRegistered ? "Edit Your Registration!" : "Registration Time!")
                .WithDescription(userHasRegistered
                ? DialogueDict.Get("REGISTRATION_EDIT", user.Queen(client))
                : DialogueDict.Get("REGISTRATION_WELCOME", config.GetDefaultPrefix()));

            var firstResponse = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: new List<IEmote>() { new Emoji("✅"), CancelEmote});
            if (firstResponse.emoteResponse.ToString().Equals(CancelEmote.ToString()))
            {
                await channel.SendMessageAsync(embed: GetEmbed()
                    .WithTitle("'Kay, Laters Then")
                    .WithDescription(DialogueDict.Get("SESSION_CANCEL"))
                    .WithImageUrl(null)
                    .Build());
                return;
            }

            embed = GetEmbed()
                .WithDescription("");
            for (int i = 0; i < 3; i++)
            {
                embed.Description = await SetUpShip(connection, (UserShipTier)i, embed);
            }

            var key = "REGISTRATION_" + (userHasRegistered ? "EDITED" : "COMPLETE") + (GameHelper.EventStarted ? "" : "_PREREG");
            embed = (await GetEmbed(dbUser, dbUserShips))
                .WithTitle("Setup Complete!")
                .WithDescription(DialogueDict.Get(key, config.GetDefaultPrefix()));
            await channel.SendMessageAsync(embed: embed.Build());
            if (!userHasRegistered)
            {
                dbUser.ShipsSelected = true;
                await repo.UpdateUserAsync(connection, dbUser);
            }
            
        }

        // Returns prefix for next message
        async Task<string> SetUpShip(SqlConnection connection, UserShipTier tier, EmbedBuilder embed)
        {
            var isNewShip = !dbUserShips.Has(tier);

            var title = $"{tier} Pair Setup";
            var tierKey = userHasRegistered ? "EDIT" : tier.ToString().ToUpper();
            embed.Description += "\n\n" + DialogueDict.Get($"REGISTRATION_ENTER_{tierKey}", tier.ToString().ToLower(), GameHelper.GetPointPercent(tier));

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
                    var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, $"{inputResult.ErrorMessage}",
                        client, showUser: false);
                    await channel.SendMessageAsync(embed: errorEmbed.Build());
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
                selectedUserShip.Heart1 = ((Emote)response.emoteResponse).Name;
                await repo.UpdateUserShipAsync(connection, selectedUserShip);
                embed = embed
                    .WithDescription(DialogueDict.Get("REGISTRATION_HEART_CHOOSE", selectedUserShip.Character2First, SkipEmote))
                    .WithImageUrl(await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier, highlightHeart: 2));
                response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
                if (!response.IsSkipped)
                {
                    selectedUserShip.Heart2 = ((Emote)response.emoteResponse).Name;
                    await repo.UpdateUserShipAsync(connection, selectedUserShip);
                }

            }

            return DialogueDict.Get("REGISTRATION_FINISH_SHIP", user.Queen(client));
        }

        async Task<Result> ProcessPairingInputAsync(SqlConnection connection, string shipStr, UserShipTier tier)
        {
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
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_YURIKO"));
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
                .WithDescription(DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE",
                ((UserShipTier)duplicateShip.Tier).ToString().ToLower(), duplicateShip.GetDisplayName(), currentTier.ToString().ToLower()))
                .WithTitle("Pairing Conflict");
            var yesEmote = new Emoji("✅");
            var noEmote = new Emoji("❌");
            var response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: new List<IEmote>() { yesEmote, noEmote });
            if (response.emoteResponse.ToString().Equals(yesEmote.ToString()))
            {
                await repo.SwapShipTiersAsync(connection, dbUser.UserId, (UserShipTier)duplicateShip.Tier, currentTier);
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
                return ValueResult<bool>.Success(true);
            }
            else
                return ValueResult<bool>.Error("Suuuuuucks");
        }

        // Returns null if ship doesn't need to be modified
        async Task<ValueResult<Ship>> ParseAndValidateShipAsync(SqlConnection connection, string shipStr, UserShipTier tier, UserShipCollection currentShips)
        {
            using var typingState = channel.EnterTypingState();
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_FORMAT"));

            var dbCharacters = await repo.GetAllCharactersAsync(connection);

            var char1 = FindMatch(split[0], dbCharacters);
            var char2 = FindMatch(split[1], dbCharacters);

            if (char1 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[0]));
            if (char2 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[1]));

            var shipKey = await repo.GetOrCreateShipAsync(connection, char1.CharacterId, char2.CharacterId);
            var ship = await repo.GetShipAsync(connection, shipKey);

            // Validation time

            if (ship.CharacterId1.Equals("YURIKO") || ship.CharacterId2.Equals("YURIKO"))
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_YURIKO"));
            if (char1.CharacterId.Equals(char2.CharacterId) && !char1.CharacterId.Equals("TSUCHINOKO"))
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_SELF"));
            if (ship.IsBlacklisted)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));
            var categories = new string[] { char1.Category, char2.Category };
            if (categories.Contains("AMBIGUOUS"))
            {
                var compatibleFields = new string[] { "AMBIGUOUS", "ADULT", "CHILD" };
                if (!compatibleFields.Contains(char1.Category ) || !compatibleFields.Contains(char2.Category))
                    return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));
            }
            else if (!char1.Category.Equals(char2.Category))
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));

            if (!string.IsNullOrWhiteSpace(char1.Family) && char1.Family.Equals(char2.Family))
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));


            // Handle tier duplicates and swapping
            var duplicateShip = currentShips
                .FirstOrDefault(a => a.Tier != (int)tier && a.ShipId.Equals(ship.ShipId));
            if (duplicateShip != null)
            {
                if (duplicateShip.Tier == (int)UserShipTier.Primary && !dbUserShips.Has(tier))
                    return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_PRIMARY_DUPE"));
                var embed = GetEmbed()
                    .WithDescription(DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE",
                    ((UserShipTier)duplicateShip.Tier).ToString().ToLower(), duplicateShip.GetDisplayName(), tier.ToString().ToLower()))
                    .WithTitle("Pairing Conflict");
                var yesEmote = new Emoji("✅");
                var noEmote = new Emoji("❌");
                typingState.Dispose();
                var response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: new List<IEmote>() { yesEmote, noEmote });
                if (response.emoteResponse.ToString().Equals(yesEmote.ToString()))
                {
                    await repo.SwapShipTiersAsync(connection, dbUser.UserId, (UserShipTier)duplicateShip.Tier, tier);
                    dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
                    return ValueResult<Ship>.Success(null);
                }
                else
                    return ValueResult<Ship>.Error("Suuuuuucks");

            }

            return ValueResult<Ship>.Success(ship);
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

        EmbedBuilder GetEmbed()
            => EmbedHelper.GetEventEmbed(user, config, showUser: false)
            .WithThumbnailUrl("https://cdn.discordapp.com/attachments/419187329706491905/843048501458108436/unknown.png");

        async Task<EmbedBuilder> GetEmbed(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0)
        => GetEmbed()
            .WithImageUrl(await GenerateShipImage(dbUser, dbShips, highlightTier, highlightHeart));

        public async Task<string> GenerateShipImage(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0)
        {
            var image = await shipImageGenerator.WriteUserAvatarAsync(dbUser, dbShips, highlightTier, highlightHeart);
            return config.GetRelativeHostPathWeb(image);
        }

        protected override string GetTimeoutMessage() => base.GetTimeoutMessage() + "\n\n" + DialogueDict.Get("SESSION_TIMEOUT_SAVED");
    }
}
