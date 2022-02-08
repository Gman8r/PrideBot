using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;
using Discord.Webhook;
using System.Net;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PrideBot.Events;
using PrideBot.Registration;
using PrideBot.Repository;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using PrideBot.Models;

namespace PrideBot.Modules
{
    [RequireOwner]
    [DontAutoLoad]
    [Name("Secret (Stealth)")]
    public class SecretStealthModule : PrideModuleBase
    {
        private IConfigurationRoot config;
        private CommandHandler commandHandler;
        readonly ModelRepository repo;

        public SecretStealthModule(IConfigurationRoot config, CommandHandler commandHandler, ModelRepository repo)
        {
            this.config = config;
            this.commandHandler = commandHandler;
            this.repo = repo;
        }

        [Command("restore")]
        async Task Restore()
        {
            // part 1
            using var typing = Context.Channel.EnterTypingState();
            ulong startMessageId = 939754618932584528;
            var channel = Context.Client.GetGuild(932055111583293500).GetTextChannel(932055114078908543);

            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var allShips = await repo.GetAllShipsAsync(connection);
            var allScores = await repo.GetAllScoresAsync(connection);
            //var allUsers = await repo.GetAllUsersAsync(connection);

            var cachedMults = new Dictionary<string, decimal>();
            try
            {
                foreach (var score in allScores.Where(a => a.ScoreId >= 16145 && a.AchievementId.Equals("CHAT")))
                {
                    var user = await repo.GetOrCreateUserAsync(connection, score.UserId);
                    var userShips = await repo.GetUserShipsAsync(connection, user.UserId);
                    foreach (var ship in userShips)
                    {

                        decimal pointsEarned = score.PointsEarned;
                        decimal rarityMult;
                        if (cachedMults.ContainsKey(ship.ShipId))
                        {
                            rarityMult = cachedMults[ship.ShipId];
                        }
                        else
                        {
                            rarityMult = await repo.GetScoreBalanceMultForShip(connection, ship.ShipId, score.Timestamp, score.UserId, -1);
                            cachedMults[ship.ShipId] = rarityMult;
                        }
                        pointsEarned *= rarityMult;

                        var shipScore = new ShipScore()
                        {
                            ScoreId = score.ScoreId,
                            Tier = ship.Tier,
                            ShipId = ship.ShipId,
                            PointsEarned = pointsEarned,
                            BonusMult = 1m,
                            TierMult = ship.Tier == 0 ? 1m : (ship.Tier == 1 ? .4m : .2m),
                            BalanceMult = rarityMult
                        };
                        var result = await DatabaseHelper.GetInsertCommand(connection,
                            shipScore, "SHIP_SCORES").ExecuteNonQueryAsync();

                        if (result < 1)
                        {

                        }
                    }
                }
            }
            catch(Exception e)
            {

            }













            return;
            while (true)
            {

                var messages = (await channel.GetMessagesAsync(startMessageId - 1, Direction.After).FlattenAsync())
                    .OrderBy(a => a.Timestamp);
                if (startMessageId == messages.Last().Id)
                    break;
                startMessageId = messages.Last().Id;

                foreach (var message in messages)
                {
                    try
                    {
                        if (!message.Author.IsBot || (message.Embeds?.First().Fields.Count() ?? 0) == 0)
                            continue;
                        var embed = message.Embeds.First();
                        var scoreId = embed
                            .Footer.Value.ToString().Split().First();
                        var score = await repo.GetScoreAsync(connection, scoreId);
                        if (score == null)
                            continue;
                        var timestamp = embed
                            .Timestamp.Value.ToDateTime();

                        var fieldText = embed.Fields.First().Value.ToString();
                        var scoreLines = fieldText.Split('\n').Skip(1).ToList();
                        foreach (var line in scoreLines)
                        {
                            string parseAsterisks(string str)
                            {
                                return new string(str
                                .Skip(2)
                                .SkipLast(2)
                                .ToArray());
                            }
                            var words = (line.Split()).ToList();
                            // parse **'s
                            var pointsEarned =
                                int.Parse(parseAsterisks(words[1]));
                            var shipName = parseAsterisks(words[3]);

                            //var chars = new List<string>() { shipName.Split('X')[0], shipName.Split('X')[1] };
                            var ship = allShips
                                .FirstOrDefault(a => a.GetDisplayName()
                                .Equals(shipName));

                            var tier = 0;
                            if (line.Contains("Secondary"))
                                tier = 1;
                            else  if (line.Contains("Tertiary"))
                                tier = 2;

                            var replacedLine = line
                                .Replace("Secondary", "Primary")
                                .Replace("Tertiary", "Primary");
                            decimal GetEffectMult(string effectName)
                            {
                                var regex = new Regex(@"\(x?([0-9]*.[0-9]*)x? "
                                    + effectName + @"\)");
                                var match = regex.Match(replacedLine);
                                if (!match.Success)
                                    return 1m;
                                else
                                    return decimal.Parse(match.Groups.Values
                                        .Last().Value);

                            }

                            try
                            {
                                var tierMult = GetEffectMult("Primary");
                                var plushieMult = GetEffectMult("Plushie Effects");
                                var rarityMult = GetEffectMult("Rarity Mult");

                                var shipScore = new ShipScore()
                                {
                                    ScoreId = int.Parse(scoreId),
                                    Tier = tier,
                                    ShipId = ship.ShipId,
                                    PointsEarned = pointsEarned,
                                    BonusMult = plushieMult,
                                    TierMult = tierMult,
                                    BalanceMult = rarityMult
                                };
                                var result = await DatabaseHelper.GetInsertCommand(connection,
                                    shipScore, "SHIP_SCORES").ExecuteNonQueryAsync();

                                if (result < 1)
                                {

                                }
                            }
                            catch (Exception e)
                            {

                            }
                        }

                    }
                    catch (Exception e)
                    {

                    }
                }
            }
            var x = 0;
        }

        [Command("lambda")]
        [RequireContext(ContextType.Guild)]
        public async Task What()
        {
            //var thread = await (Context.Channel as ITextChannel).CreateThreadAsync("robobot thread", ThreadType.PrivateThread, invitable: false);
            //var user = Context.Guild.GetUser(253190116972036097);
            //await thread.SendMessageAsync($"hi!! {user.Mention} hi!!!");
        }

        [Command("applyroles")]
        [RequireContext(ContextType.Guild)]
        public async Task Haha()
        {
            using var typing = Context.Channel.EnterTypingState();
            await ReapplyRoles(Context.Guild);
            await ReplyAsync("Applying...");
        }

        //[Command("applymults")]
        //public async Task ApplyMults()
        //{
        //    using var typing = Context.Channel.EnterTypingState();
        //    using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
        //    var allScores = await repo.GetAllShipScoresAsync(connection);
        //    var count = allScores.Count();
        //    var i = 0;
        //    foreach (var shipScore in allScores)
        //    {
        //        i++;
        //        if (shipScore.AchievementId.Contains("REGISTER")
        //            || !MathHelper.Approximately(shipScore.BalanceMult, 1m, .01m))
        //            continue;
        //        var newBalanceMult = await repo.GetScoreBalanceMultForShip(connection, shipScore.ShipId, shipScore.Timestamp, shipScore.UserId);
        //        if (MathHelper.Approximately(newBalanceMult, 1.0m, .01m))
        //            continue;
        //        shipScore.BalanceMult = newBalanceMult;
        //        shipScore.PointsEarned *= newBalanceMult;


        //        var query = $"update dbo.SHIP_SCORES set BALANCE_MULT = {shipScore.BalanceMult}, POINTS_EARNED = {shipScore.PointsEarned}" +
        //            $" where SCORE_ID = {shipScore.ScoreId} and TIER = {shipScore.Tier}";
        //        await new SqlCommand(query,connection).ExecuteNonQueryAsync();


        //        Console.WriteLine(i + "/" + count);
        //    }
        //    await ReplyAsync("Done!");
        //}

        [Command("applybans")]
        [RequireContext(ContextType.Guild)]
        public async Task bannn()
        {
            await ReapplyBans(Context.Guild);
            await ReplyAsync("Applying...");
        }

        [Command("applyemotes")]
        [RequireContext(ContextType.Guild)]
        public async Task emote()
        {
            await ReapplyEmotes(Context.Guild);
            await ReplyAsync("Applying...");
        }

        [Command("event")]
        [RequireContext(ContextType.Guild)]
        public async Task PlushieEvent()
        {
            var guild = Context.Client.GetGyn(config);
            await guild.CreateEventAsync("The Great Plushie Singularity",
                new DateTime(2022, 2, 14), GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private,
                "At the peak of love and destiny, life can begin anew. 💗",
                new DateTime(2022, 2, 15), null, "All of GYN, all of Gensokyo");
        }

        [Command("postteaser")]
        [Alias("postteaser")]
        public async Task PostTeaser(ITextChannel channel, string username, string content)
        {
            var webhook = (await channel.GetWebhooksAsync())
                .FirstOrDefault(a => a.Creator.Id == Context.Client.CurrentUser.Id);
            if (webhook == null)
            {
                var imageStream = new MemoryStream(await File.ReadAllBytesAsync("black.png"));
                imageStream.Seek(0, SeekOrigin.Begin);
                webhook = await channel.CreateWebhookAsync("The Rift", imageStream);
            }

            var client = new DiscordWebhookClient(webhook);
            await client.SendMessageAsync(content, username: username);
        }

        async Task ReapplyBans(SocketGuild guild)
        {
            var dataStr = await File.ReadAllTextAsync("guild.json");
            var data = JsonConvert.DeserializeObject<dynamic>(dataStr);
            var bans = data.bans;
            var guildBans = await guild.GetBansAsync();
            foreach (var ban in bans)
            {
                ulong id = (ulong)ban.id;
                var guildBan = guildBans.FirstOrDefault(a => a.User.Id == id);
                if (guildBan != null)   // already banned
                    continue;

                string reason = ban.reason;
                if (string.IsNullOrWhiteSpace(reason))
                    reason = null;

                Console.WriteLine("Banning " + ban.name);
                await guild.AddBanAsync(id, 0, reason);
            }
        }

        async Task ReapplyEmotes(SocketGuild guild)
        {
            var dataStr = await File.ReadAllTextAsync("guild.json");
            var data = JsonConvert.DeserializeObject<dynamic>(dataStr);
            var emotes = data.emotes;
            var guidldEmotes = guild.Emotes;
            foreach (var emote in emotes)
            {
                string name = emote.name;
                string url = emote.url;
                var guildEmote = guidldEmotes.FirstOrDefault(a => a.Name.Equals(name));
                if (guildEmote != null)   // already added
                    continue;

                try
                {
                    var extension = url.Substring(url.Length - 4);
                    var imageData = await WebHelper.DownloadWebFileDataAsync(url);
                    var path = "emotes/" + name + extension;
                    if (File.Exists(path))
                    {

                    }
                    await File.WriteAllBytesAsync(path, imageData);
                    //stream.Seek(0, SeekOrigin.Begin);
                    //await guild.CreateEmoteAsync(name, new Image(stream));
                }
                catch (Exception e)
                {
                    Console.WriteLine("COULDN'T GET " + emote.name);
                }
            }
        }



        async Task ReapplyRoles(SocketGuild guild)
        {
            var dataStr = await File.ReadAllTextAsync("guild.json");
            var data = JsonConvert.DeserializeObject<dynamic>(dataStr);
            var users = data.users;
            var botUser = Context.Guild.GetUser(Context.Client.CurrentUser.Id);
            var tasks = new List<Task>();
            foreach (var user in data.users)
            {
                ulong id = (ulong)user.id;
                //if (!guildUsers.Any(a => a.Id == id))
                //    continue;
                var guildUser = guild.GetUser(id);
                if (guildUser == null || guildUser.IsBot)
                    continue;
                var roles = user.roles;

                var task = Task.Run(async () =>
                {
                    var guildRoles = new List<SocketRole>();
                    // figure out roles from names
                    foreach (var role in roles)
                    {
                        string name = role.name;
                        if (name.Equals("@everyone"))
                            continue;

                        var guildRole = guild.Roles
                            .FirstOrDefault(a => a.Name.Equals(name));
                        if (guildRole == null)
                            continue;
                        if (guildRole.Position >= botUser.Roles.Max(a => a.Position))
                            continue;

                        guildRoles.Add(guildRole);
                    }

                    Console.WriteLine("Applying for " + guildUser.Username);
                    await guildUser.AddRolesAsync(guildRoles);
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        [Command("write")]
        async Task Write(ulong guildID)
        {
            var guild = Context.Client.GetGuild(guildID);
            await WriteGuild(guild);
            await ReplyResultAsync("Done!");
        }



        public static async Task WriteGuild(SocketGuild guild)
        {
            var guildDict = new Dictionary<string, object>();
            var users = new List<Dictionary<string, object>>();
            var emotes = new List<Dictionary<string, object>>();
            var bans = new List<Dictionary<string, object>>();
            guildDict["name"] = guild.Name;
            guildDict["users"] = users;
            guildDict["emotes"] = emotes;
            guildDict["bans"] = bans;
            foreach (var user in guild.Users)
            {
                var userDict = new Dictionary<string, object>();
                users.Add(userDict);
                userDict["id"] = user.Id;
                userDict["nickname"] = user.Nickname;
                userDict["username"] = user.Username;
                userDict["discriminator"] = user.Discriminator;
                userDict["fullname"] = user.Username + "#" + user.Discriminator;
                userDict["bot"] = user.IsBot;

                var roles = new List<Dictionary<string, object>>();
                userDict["roles"] = roles;
                foreach (var role in user.Roles)
                {
                    var roleDict = new Dictionary<string, object>();
                    roles.Add(roleDict);
                    roleDict["id"] = role.Id;
                    roleDict["name"] = role.Name;
                }
            }
            foreach (var emote in guild.Emotes)
            {
                var emoteDict = new Dictionary<string, object>();
                emotes.Add(emoteDict);
                emoteDict["id"] = emote.Id;
                emoteDict["url"] = emote.Url;
                emoteDict["name"] = emote.Name;
                emoteDict["animated"] = emote.Animated;
            }
            var guildBans = await guild.GetBansAsync();
            foreach (var guildBan in guildBans)
            {
                var banDict = new Dictionary<string, object>();
                bans.Add(banDict);
                banDict["id"] = guildBan.User?.Id;
                banDict["username"] = guildBan.User?.Username;
                banDict["discriminator"] = guildBan.User?.Discriminator;
                banDict["fullname"] = guildBan.User.Username + "#" + guildBan.User.Discriminator;
                banDict["reason"] = guildBan.Reason;
            }

            var a = 0;

            var jsonString = JsonConvert.SerializeObject(guildDict, Formatting.Indented);
            File.WriteAllText($"guilds/{guild.Id} {guild.Name.Substring(0, 5)}.json", jsonString);
            Console.WriteLine("Wrote " + guild.Name);
        }

        //[Command("hook")]
        //[Alias("mimic", "possess")]
        //[Priority(1)]
        //public async Task HookMimic(ulong userId = 0)
        //{
        //    if (userId == 0)
        //    {
        //        chatSession.SetWebhookData(null, null);
        //        await ReplyResultAsync("Stopped mimicking.");
        //        return;
        //    }
        //    var user = chatSession.Channel.Guild.GetUser(userId);
        //    chatSession.SetWebhookData(user.GetAvatarUrl(), user.Nickname ?? user.Username);
        //    await ReplyResultAsync($"Mimicking {user.Nickname ?? user.Username}.");
        //}

        //[Command("hook")]
        //[Alias("mimic", "possess")]
        //[Priority(0)]
        //public async Task HookMimic(string name)
        //{
        //    if (Context.Message.Attachments.Any())
        //    {
        //        var avatarUrl = Context.Message.Attachments.FirstOrDefault().Url;
        //        await HookMimic(avatarUrl, name);
        //        return;
        //    }
        //    var user = chatSession.Channel.Guild.Users.FirstOrDefault(a => (a.Nickname ?? a.Username).Contains(name, StringComparison.OrdinalIgnoreCase));
        //    chatSession.SetWebhookData(user.GetAvatarUrl(), user.Nickname ?? user.Username);
        //    await ReplyResultAsync($"Mimicking {user.Nickname ?? user.Username}.");
        //}

        //[Command("hook")]
        //[Alias("mimic", "possess")]
        //public async Task HookMimic(string avatarUrl, [Remainder]string name)
        //{
        //    var avatarFile = await new FileDownloader(config.GetRelativeFilePath("temp"))
        //        .DownloadFileAsync(avatarUrl);
        //    //Stream imageStream = new FileStream(avatarFile, FileMode.Open);
        //    //var webhook = await chatSession.Channel.CreateWebhookAsync(name, imageStream);
        //    chatSession.SetWebhookData(avatarUrl, name);
        //    await ReplyResultAsync($"Mimicking {name}.");
        //}

        //[Command("react")]
        //public async Task React(int index, string emoji)
        //{
        //    Emote emote;
        //    IEmote iemote;
        //    if (Emote.TryParse(emoji, out emote))
        //        iemote = emote;
        //    else
        //        iemote = new Emoji(emoji);

        //    var ch = chatSession.Channel;
        //    var msg = (await ch.GetMessagesAsync().FlattenAsync()).ToArray()[index];
        //    await msg.AddReactionAsync(iemote);
        //}

        //[Command("react")]
        //public async Task React(string emoji)
        //    => await React(0, emoji);

        //[Command("emote")]
        //public async Task EmoteText(int index, [Remainder]string text)
        //{
        //    var messageIndex = @"🇦🇧🇨🇩🇪🇫🇬🇭🇮🇯🇰🇱🇲🇳🇴🇵🇶🇷🇸🇹🇺🇻🇼🇽🇾🇿";
        //    text = string.Join("", text.Split());
        //    var arr = text.ToLower().Select(a => messageIndex.Substring((a - 'a') * 2, 2));

        //    var ch = chatSession.Channel;
        //    var msg = (await ch.GetMessagesAsync().FlattenAsync()).ToArray()[index];
        //    //msg = Context.Message;
        //    foreach (var chr in arr)
        //    {
        //        await msg.AddReactionAsync(new Emoji(chr.ToString()));
        //    }
        //}

        [Command("mayday")]
        public async Task Auditlog()
        {
            foreach (var guild in Context.Client.Guilds)
            {
                commandHandler.RelayMessageAsync(guild);
            }
            await ReplyAsync("omg... on it.");
        }

        //[Command("emote")]
        //public async Task EmoteText(string text)
        //    => await EmoteText(0, text);

        //[Command("bubble")]
        //[Alias("bubbletext")]
        //public async Task Bubble([Remainder] string text)
        //{
        //    text = MessageHelper.ToBubbleText(text);
        //    await chatSession.SendMessageAsync(text);
        //}
    }
}