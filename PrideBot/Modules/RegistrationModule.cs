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
        [Summary("Register with a ship for the event, or change your ship. For arguments, use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter.")]
        public async Task Register([Remainder][Name("ship")]string shipStr)
        {
            if (string.IsNullOrWhiteSpace(shipStr))
                throw new CommandException("You need to input a ship.");


            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

            var shipValues = await ParseShipAsync(connection, shipStr);
            ValidateShip(shipValues.Item1, shipValues.Item2);
            shipValues = OrderShip(shipValues);

            var ship = await repo.GetShipAsync(connection, shipValues.Item1.CharacterId, shipValues.Item2.CharacterId);
            if (ship == null)
            {
                using var typingState = Context.Channel.EnterTypingState();

                // Create new ship
                ship = new Ship()
                {
                    CharacterId1 = shipValues.Item1.CharacterId,
                    CharacterId2 = shipValues.Item2.CharacterId
                };
                var image = await ImageEditingHelper.GenerateShipAvatarAsync(ship.CharacterId1, ship.CharacterId2);

                //var fileDumpChannel = Context.Client
                //    .GetGuild(ulong.Parse(config["ids:filedumpguild"]))
                //    .GetTextChannel(ulong.Parse(config["ids:filedumpchannel"])) as ISocketMessageChannel;
                //var avatarMsg = await fileDumpChannel.SendFileAsync(image.Stream, image.FileName);

                var uploadPath = Path.Combine(config["paths:wwwhostpathlocal"], "ships", $"{ship.CharacterId1}X{ship.CharacterId2}.png");
                await File.WriteAllBytesAsync(uploadPath, image.Stream.ToArray());
                var webPath = config["paths:wwwhostpath"] + $"/ships/{ship.CharacterId1}X{ship.CharacterId2}.png";

                ship.AvatarUrl = webPath;
                await repo.AddShipAsync(connection, ship);
            }

            string descr;
            string title;
            var user = await repo.GetUserAsync(connection, Context.User.Id.ToString());
            if (user == null)
            {
                user = new User()
                {
                    UserId = Context.User.Id.ToString(),
                    CharacterId1 = ship.CharacterId1,
                    CharacterId2 = ship.CharacterId2
                };

                await repo.AddUserAsync(connection, user);
                title = "Registered!";
                descr = $"You're in! You are supporting **{ship.GetDisplayName()}**. Enjoy the event!";
            }
            else
            {
                user.CharacterId1 = ship.CharacterId1;
                user.CharacterId2 = ship.CharacterId2;
                await repo.UpdateUserAsync(connection, user);
                title = "Ship Updated!";
                descr = $"You are now supporting **{ship.GetDisplayName()}**.";
            }

            var url = Context.User.GetAvatarUrlOrDefault().Split('?')[0];
            var embed = EmbedHelper.GetEventEmbed(Context, config, user.UserId)
                .WithTitle(title)
                .WithDescription(descr)
                .WithThumbnailUrl(ship.AvatarUrl);
            await ReplyAsync(embed: embed.Build());
        }

        public void ValidateShip(Character char1, Character char2)
        {

            if (char1.Family != null && char1.Family.Equals(char2.Family))
                throw new CommandException("That ship is not valid for this event.");
            if (!char1.Category.Equals(char2.Category))
                throw new CommandException("That ship is not valid for this event.");
            if (char1.CharacterId.Equals(char2.CharacterId))
                throw new CommandException("Wait hold on now! Let's hope nobody's that narcissistic!");
        }


        public (Character, Character) OrderShip((Character, Character) chars)
        // Order alphabetically
        => chars.Item1.Name.CompareTo(chars.Item2.Name) <= 0 ? chars : (chars.Item2, chars.Item1);

        async Task<(Character, Character)> ParseShipAsync(SqlConnection connection, string shipStr)
        {
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                throw new CommandException("That ship name isn't formatted correctly. Use the format: `Character One X Character Two`. Either first or full names are fine, and order doesn't matter!");

            var characters = await repo.GetAllCharactersAsync(connection);
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