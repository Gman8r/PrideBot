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

namespace PrideBot.Modules
{
    [RequireSage]
    [DontAutoLoad]
    [Name("Secret (Stealth)")]
    public class SecretStealthModule : PrideModuleBase
    {
        private IConfigurationRoot config;
        private CommandHandler commandHandler;

        public SecretStealthModule(IConfigurationRoot config, CommandHandler commandHandler)
        {
            this.config = config;
            this.commandHandler = commandHandler;
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
        public async Task Haha(params IGuildUser[] users)
        {
            using var typing = Context.Channel.EnterTypingState();
            await ReapplyRoles(Context.Guild, users);
            await ReplyResultAsync("Donnnneeeeee!");
        }

        [Command("applybans")]
        [RequireContext(ContextType.Guild)]
        public async Task bannn()
        {
            await ReapplyBans(Context.Guild);
            await ReplyResultAsync("Donnnneeeeee!");
        }

        [Command("applyemotes")]
        [RequireContext(ContextType.Guild)]
        public async Task emote()
        {
            await ReapplyEmotes(Context.Guild);
            await ReplyResultAsync("Donnnneeeeee!");
        }

        //[Command("postteaser")]
        //[Alias("postteaser")]
        //public async Task PostTeaser(ITextChannel channel, string username, string content)
        //{
        //    var webhook = (await channel.GetWebhooksAsync())
        //        .FirstOrDefault(a => a.Creator.Id == Context.Client.CurrentUser.Id);
        //    if (webhook == null)
        //    {
        //        var imageStream = new MemoryStream(await File.ReadAllBytesAsync("black.png"));
        //        imageStream.Seek(0, SeekOrigin.Begin);
        //        webhook = await channel.CreateWebhookAsync("A Rift Has Opened", imageStream);
        //    }

        //    var client = new DiscordWebhookClient(webhook);
        //    await client.SendMessageAsync(content, username: username);
        //}

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



        async Task ReapplyRoles(SocketGuild guild, IEnumerable<IGuildUser> guildUsers)
        {
            var dataStr = await File.ReadAllTextAsync("guild.json");
            var data = JsonConvert.DeserializeObject<dynamic>(dataStr);
            var users = data.users;
            var botUser = Context.Guild.GetUser(Context.Client.CurrentUser.Id);
            var tasks = new List<Task>();
            foreach (var user in data.users)
            {
                ulong id = (ulong)user.id;
                if (!guildUsers.Any(a => a.Id == id))
                    continue;
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