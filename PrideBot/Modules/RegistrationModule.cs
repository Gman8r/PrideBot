using Discord;
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

namespace PrideBot.Modules
{
    [Name("Registration")]
    [RequireOwner]
    [RequireContext(ContextType.Guild)]
    public class RegistrationModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;

        public RegistrationModule(ModelRepository modelRepository, IConfigurationRoot config)
        {
            this.repo = modelRepository;
            this.config = config;
        }

        [Command("register")]
        public async Task Register([Remainder][Name("ship")]string shipStr)
        {
            if (string.IsNullOrWhiteSpace(shipStr))
                throw new CommandException("You need to input a ship.");


            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

            var shipValue = await ParseShipAsync(connection, shipStr);
            ValidateShip(shipValue.Item1, shipValue.Item2);
            shipValue = OrderShip(shipValue);

            string descr;
            string title;
            var user = await repo.GetUser(connection, Context.User.Id.ToString());
            if (user == null)
            {
                user = new User()
                {
                    UserId = Context.User.Id.ToString(),
                    CharacterId1 = shipValue.Item1.CharacterId,
                    CharacterId2 = shipValue.Item2.CharacterId
                };

                await repo.AddUser(connection, user);
                title = "Registered!";
                descr = $"Registered! You are supporting **{shipValue.Item1.Name} X {shipValue.Item2.Name}**";
            }
            else
            {
                user.CharacterId1 = shipValue.Item1.CharacterId;
                user.CharacterId2 = shipValue.Item2.CharacterId;
                await repo.UpdateUser(connection, user);
                title = "Ship Updated!";
                descr = $"You are now supporting **{shipValue.Item1.Name} X {shipValue.Item2.Name}**";
            }

            var url = Context.User.GetAvatarUrlOrDefault().Split('?')[0];
            var embed = EmbedHelper.GetEventEmbed(Context, config, user.UserId)
                .WithTitle(title)
                .WithDescription(descr);
            await Context.Message.ReplyAsync(embed: embed.Build());
        }

        public void ValidateShip(Character char1, Character char2)
        {

            if (char1.Family != null && char1.Family.Equals(char2.Family))
                throw new CommandException("That ship is not valid for this event.");
            if (!char1.Category.Equals(char2.Category))
                throw new CommandException("That ship is not valid for this event.");
        }


        public (Character, Character) OrderShip((Character, Character) chars)
        // Order alphabetically
        => chars.Item1.Name.CompareTo(chars.Item2.Name) <= 0 ? chars : (chars.Item2, chars.Item1);

        async Task<(Character, Character)> ParseShipAsync(SqlConnection connection, string shipStr)
        {
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                throw new CommandException("That ship name isn't formatted correctly. Use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter!");

            var characters = await repo.GetAllCharacters(connection);
            var match = (FindMatch(split[0], characters), FindMatch(split[1], characters));

            if (match.Item1 == null)
                throw new CommandException($"I couldn't find a match for {split[0]} in my character records.");
            if (match.Item2 == null)
                throw new CommandException($"I couldn't find a match for {split[1]} in my character records.");

            return match;
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
    }
}