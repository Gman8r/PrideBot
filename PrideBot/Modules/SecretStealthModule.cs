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
    [RequireOwner]
    [DontAutoLoad]
    [Name("Secret (Stealth)")]
    public class SecretStealthModule : PrideModuleBase
    {
        private IConfigurationRoot config;

        public SecretStealthModule(IConfigurationRoot config)
        {
            this.config = config;
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

        //[Command("auditlog")]
        //public async Task Auditlog(ulong id, string actionType)
        //{
        //    var logs = (await Context.Guild.GetAuditLogsAsync(100, userId: 111231140462866432, actionType: (ActionType)Enum.Parse(typeof(ActionType), actionType)).FlattenAsync())
        //        .Select(a => JsonConvert.SerializeObject(a.Data));

        //    var msg = "```\n" + string.Join("\n\n", logs) + "```\n";
        //    await ReplyAsync(msg);
        //}

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