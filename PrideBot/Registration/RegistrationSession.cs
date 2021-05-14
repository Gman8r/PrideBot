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

        bool userHasRegistered;
        User dbUser;
        UserShipCollection dbUserShips;
        IEnumerable<Character> dbCharacters;

        public RegistrationSession(IDMChannel channel, IUser user, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, ModelRepository repo, DiscordSocketClient client) : base(channel, user, config, client)
        {
            this.shipImageGenerator = shipImageGenerator;
            this.repo = repo;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {
            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

            dbUser = await repo.GetUserAsync(connection, user.Id.ToString());
            dbUserShips = new UserShipCollection();
            dbCharacters = await repo.GetAllCharactersAsync(connection);
            if (dbUser == null)
            {
                dbUser = new User() { UserId = user.Id.ToString() };
                await repo.AddUserAsync(connection, dbUser);
            }
            else
            {
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
            }
            userHasRegistered = dbUser.ShipsSelected;

            for (int i = 0; i < 3; i++)
            {
                var ship = await SetUpShip(connection, (UserShipTier)i);
                if (ship != null)
                    dbUserShips.Set((UserShipTier)i, ship);
            }

            var embed = (await GetEmbed(dbUser, dbUserShips))
                .WithTitle($"Yayyyy!")
                .WithDescription("u did it!\nYou can do it again and change stuff with the same command, but it's kinda lengthy, better editing system planned for later." +
                "\nI'm gonna let you change the background too but I don't have any other ones rn, help wanted!" +
                $"\nUse `{config.GetDefaultPrefix()}ships` to show off your pairings in the server.");
            await channel.SendMessageAsync(embed: embed.Build());
            if (!userHasRegistered)
            {
                dbUser.ShipsSelected = true;
                await repo.UpdateUserAsync(connection, dbUser);
            }
            
        }

        async Task<UserShip> SetUpShip(SqlConnection connection, UserShipTier tier)
        {
            var userShipExistsDb = dbUserShips.Has(tier);
            var userShip = dbUserShips.Get(tier)
                ?? new UserShip() { UserId = dbUser.UserId, CharacterId1 = "DEFAULT", CharacterId2 = "DEFAULT", Tier = (int)tier };
            dbUserShips.Set(tier, userShip);

            bool shipValid = false;
            var embed = (await GetEmbed(dbUser, dbUserShips, (int)tier))
                .WithTitle($"{tier} Ship Setup");
            embed.Description = $"Enter {tier.ToString().ToLower()} ship. Use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter. All dialogue is placeholder.";

            // Determine what bypasses are possible
            var isDefault = userShip.CharacterId1.Equals("DEFAULT");
            var canSkip = tier != UserShipTier.Primary || userHasRegistered;
            if (canSkip)
            {
                if (isDefault)
                    embed.Description += $"\n\nReact {SkipEmote} to skip adding a {tier.ToString().ToLower()} ship.";
                else
                    embed.Description += $"\n\nReact {SkipEmote} to leave the characters as-is.";
            }

            // Now register the ship
            Prompt response;
            while (!shipValid)
            {
                response = await SendAndAwaitResponseAsync(embed: embed, canSkip: canSkip);
                if (response.IsSkipped)
                {
                    if (isDefault)
                    {
                        dbUserShips.Remove(tier);
                        return null;
                    }
                    else
                        break;
                }
                userShip.Heart1 = null;
                userShip.Heart2 = null;
                var result = await ParseAndValidateShipAsync(connection, response.MessageResponse.Content, tier, dbUserShips);
                shipValid = result.IsSuccess;
                if (!shipValid)
                    embed.Description = $"{result.ErrorMessage}\n\nPlease try again. Use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter. All dialogue is placeholder.";
                else
                {
                    // Add or update a user ship to the db and pull from it again to have full data
                    var ship = result.Value;
                    userShip.CharacterId1 = ship.CharacterId1;
                    userShip.CharacterId2 = ship.CharacterId2;
                    userShip.ShipId = ship.ShipId;
                    if (userShipExistsDb)
                        await repo.UpdateUserShipAsync(connection, userShip);
                    else
                        await repo.AddUserShipAsync(connection, userShip);
                    userShip = await repo.GetUserShipAsync(connection, userShip.UserId, userShip.Tier);
                    dbUserShips.Set(tier, userShip);
                }
            }

            var heartDescription = $"Choose a heart for **{{0}}**, or finish selecting hearts by reacting with {SkipEmote}. (I haven't added them all yet sorry)";
            var heartEmotes = client.GetGuild(796585563166736394).Emotes.Where(a => a.Name.StartsWith("shipheart"))
                .Select(a => (IEmote)a)
                .ToList();
            embed = embed
                .WithDescription(string.Format(heartDescription, userShip.Character1First))
                .WithImageUrl(await GenerateShipImage(dbUser, dbUserShips, (int)tier));
            response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
            if (!response.IsSkipped)
            {
                userShip.Heart1 = ((Emote)response.emoteResponse).Name;
                await repo.UpdateUserShipAsync(connection, userShip);
                embed = embed
                    .WithDescription(string.Format(heartDescription, userShip.Character2First))
                    .WithImageUrl(await GenerateShipImage(dbUser, dbUserShips, (int)tier));
                response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: heartEmotes, canSkip: true);
                if (!response.IsSkipped)
                {
                    userShip.Heart2 = ((Emote)response.emoteResponse).Name;
                    await repo.UpdateUserShipAsync(connection, userShip);
                }

            }

            return userShip;
        }

        async Task<ValueResult<Ship>> ParseAndValidateShipAsync(SqlConnection connection, string shipStr, UserShipTier tier, UserShipCollection currentShips)
        {
            using var typingState = channel.EnterTypingState();
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                return ValueResult<Ship>.Error("That ship name isn't formatted correctly. Use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter.");

            var char1 = FindMatch(split[0], dbCharacters);
            var char2 = FindMatch(split[1], dbCharacters);

            if (char1 == null)
                return ValueResult<Ship>.Error($"I couldn't find a match for {split[0]} in my character records.");
            if (char2 == null)
                return ValueResult<Ship>.Error($"I couldn't find a match for {split[1]} in my character records.");

            var shipKey = await repo.GetOrCreateShipAsync(connection, char1.CharacterId, char2.CharacterId);
            var ship = await repo.GetShipAsync(connection, shipKey);

            // Validation time

            if (ship.IsBlacklisted)
                return ValueResult<Ship>.Error("That ship is not valid for this event.");
            var categories = new string[] { char1.Category, char2.Category };
            if (categories.Contains("AMBIGUOUS"))
            {
                var compatibleFields = new string[] { "AMBIGUOUS", "ADULT", "CHILD" };
                if (!compatibleFields.Contains(char1.Family ) || !compatibleFields.Contains(char2.Family))
                    return ValueResult<Ship>.Error("That ship is not valid for this event.");
            }
            else if (!char1.Category.Equals(char2.Category))
                return ValueResult<Ship>.Error("That ship is not valid for this event.");

            if (!string.IsNullOrWhiteSpace(char1.Family) && char1.Family.Equals(char2.Family))
                return ValueResult<Ship>.Error("That ship is not valid for this event.");
            if (char1.CharacterId.Equals(char2.CharacterId))
                return ValueResult<Ship>.Error("Wait hold on now! Let's hope nobody's that narcissistic!");


            // Handled duplicating
            var duplicateShip = currentShips
                .FirstOrDefault(a => a.Tier != (int)tier && a.ShipId.Equals(ship.ShipId));
            if (duplicateShip != null)
            {
                if (duplicateShip.Tier == (int)UserShipTier.Primary)
                    return ValueResult<Ship>.Error("You've already chosen that for your primary tier." +
                        " The laws of love forbid deleting your primary, so you'll need to edit your primary ship to another pair first.");
                var embed = GetEmbed()
                    .WithDescription($"You've already chosen that for your {((UserShipTier)duplicateShip.Tier).ToString().ToLower()} ship." +
                    $"\nWould you like to move it to {tier.ToString().ToLower()}?");
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
                    return ValueResult<Ship>.Error("");

            }

            return ValueResult<Ship>.Success(ship);
        }


        Character FindMatch(string inputName, IEnumerable<Character> characters)
        {
            var words = inputName
                .ToUpper()
                .Split()
                .Where(a => a.Any())
                .ToArray();
            return characters
                .FirstOrDefault(a => !words.Except(a.Name.Replace("'", "").ToUpper().Split()).Any());   // All words in input name appear in character name
        }

        EmbedBuilder GetEmbed()
            => EmbedHelper.GetEventEmbed(user, config, showUser: false);

        async Task<EmbedBuilder> GetEmbed(User dbUser, UserShipCollection dbShips, int highlightTier = -1)
        => GetEmbed()
            .WithImageUrl(await GenerateShipImage(dbUser, dbShips, highlightTier));

        public async Task<string> GenerateShipImage(User dbUser, UserShipCollection dbShips, int highlightTier = -1)
        {
            var image = await shipImageGenerator.WriteUserAvatarAsync(dbUser, dbShips, highlightTier);
            return config.GetRelativeHostPathWeb(image);
        }
    }
}
