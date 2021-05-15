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

            await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: new List<IEmote>() { new Emoji("✅")});

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

        async Task<string> SetUpShip(SqlConnection connection, UserShipTier tier, EmbedBuilder embed)
        {
            var userShipExistsDb = dbUserShips.Has(tier);
            var isNewShip = !dbUserShips.Has(tier);
            var userShip = dbUserShips.Get(tier)
                ?? new UserShip() { UserId = dbUser.UserId, CharacterId1 = "DEFAULT", CharacterId2 = "DEFAULT", Tier = (int)tier };
            var displayShips = dbUserShips.Clone();
            displayShips.Set(tier, userShip);

            var title = $"{tier.ToString()} Pair Setup";
            var tierKey = userHasRegistered ? "EDIT" : tier.ToString().ToUpper();
            embed.Description += "\n\n" + DialogueDict.Get($"REGISTRATION_ENTER_{tierKey}", tier.ToString().ToLower(), GameHelper.GetPointPercent(tier));

            var enterInstructions = DialogueDict.Get("REGISTRATION_SHIP_FORMAT");
            if (isNewShip && tier != UserShipTier.Primary)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_SKIP_SHIP", SkipEmote.ToString(), tier.ToString().ToLower());
            else if (!isNewShip)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_KEEP_SHIP", SkipEmote.ToString());
            embed.Description += "\n\n" + enterInstructions;

            // Determine what bypasses are possible
            var canSkip = tier != UserShipTier.Primary || userHasRegistered;

            // Now register the ship
            var shipValidated = false;
            Prompt response;
            embed.Title = title;
            var skipped = false;
            while (!shipValidated)
            {
                embed.ImageUrl = await GenerateShipImage(dbUser, displayShips, highlightTier: (int)tier);
                response = await SendAndAwaitResponseAsync(embed: embed, canSkip: canSkip);
                if (response.IsSkipped)
                {
                    skipped = true;
                    if (isNewShip)
                    {
                        return "";
                    }
                    else
                        break;
                }
                var result = await ParseAndValidateShipAsync(connection, response.MessageResponse.Content, tier, dbUserShips);
                shipValidated = result.IsSuccess;
                if (!shipValidated)
                {
                    var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, $"{result.ErrorMessage}",
                        client, showUser: false);
                    await channel.SendMessageAsync(embed: errorEmbed.Build());
                    embed.Description = $"{DialogueDict.Get("SESSION_TRY_AGAIN")} {enterInstructions}";
                }
                else
                {
                    // Add or update a user ship to the db and pull from it again to have full data
                    var ship = result.Value;

                    //Act like the user skipped if this is the same pair
                    if (userShip.ShipId != null && userShip.ShipId.Equals(ship.ShipId))
                    {
                        skipped = true;
                        break;
                    }

                    userShip.Heart1 = null;
                    userShip.Heart2 = null;
                    userShip.CharacterId1 = ship.CharacterId1;
                    userShip.CharacterId2 = ship.CharacterId2;
                    userShip.ShipId = ship.ShipId;
                    if (userShipExistsDb)
                        await repo.UpdateUserShipAsync(connection, userShip);
                    else
                        await repo.AddUserShipAsync(connection, userShip);
                    userShip = await repo.GetUserShipAsync(connection, userShip.UserId, userShip.Tier);
                    dbUserShips.Set(tier, userShip);
                    displayShips = dbUserShips;
                }
            }

            if (!skipped)
            {
                var conirmationEmbed = GetEmbed()
                    .WithTitle(DialogueDict.Get("REGISTRATION_SHIP_ENTERED"))
                    .WithDescription(DialogueDict.Get("REGISTRATION_SHIP_REVIEW", userShip.GetDisplayName(), tier.ToString().ToLower()));
                await channel.SendMessageAsync(embed: conirmationEmbed.Build());
            }

            embed = embed.WithDescription(DialogueDict.Get("REGISTRATION_HEART_PROMPT")
                    + "\n\n" + DialogueDict.Get("REGISTRATION_HEART_CHOOSE", userShip.Character1First, SkipEmote))
                .WithImageUrl(await GenerateShipImage(dbUser, displayShips, highlightTier: (int)tier, highlightHeart: 1))
                .WithTitle(title);
            var heartEmotes = client.GetGuild(796585563166736394).Emotes.Where(a => a.Name.StartsWith("shipheart"))
                .Select(a => (IEmote)a)
                .ToList();
            response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
            if (!response.IsSkipped)
            {
                userShip.Heart1 = ((Emote)response.emoteResponse).Name;
                await repo.UpdateUserShipAsync(connection, userShip);
                embed = embed
                    .WithDescription(DialogueDict.Get("REGISTRATION_HEART_CHOOSE", userShip.Character2First, SkipEmote))
                    .WithImageUrl(await GenerateShipImage(dbUser, displayShips, highlightTier: (int)tier, highlightHeart: 2));
                response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
                if (!response.IsSkipped)
                {
                    userShip.Heart2 = ((Emote)response.emoteResponse).Name;
                    await repo.UpdateUserShipAsync(connection, userShip);
                }

            }

            return DialogueDict.Get("REGISTRATION_FINISH_SHIP", user.Queen(client));
        }

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


            // Handle duplicates
            var duplicateShip = currentShips
                .FirstOrDefault(a => a.Tier != (int)tier && a.ShipId.Equals(ship.ShipId));
            if (duplicateShip != null)
            {
                if (duplicateShip.Tier == (int)UserShipTier.Primary)
                    return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_PRIMARY_DUPE"));
                var embed = GetEmbed()
                    .WithDescription(DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE",
                    ((UserShipTier)duplicateShip.Tier).ToString().ToLower(), duplicateShip.GetDisplayName(), tier.ToString().ToLower()))
                    .WithTitle("Error");
                var yesEmote = new Emoji("✅");
                var noEmote = new Emoji("❌");
                var response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: new List<IEmote>() { yesEmote, noEmote });
                if (response.emoteResponse.ToString().Equals(yesEmote.ToString()))
                {
                    await repo.DeleteUserShipAsync(connection, dbUser.UserId, (int)tier);
                    var a = await repo.ChangeUserShipTierAsync(connection, duplicateShip.UserId, duplicateShip.Tier, (int)tier);
                    dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
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
    }
}
