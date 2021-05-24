using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;
using System.Net;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;

namespace PrideBot
{
    static class UserHelper
    {
        public static ulong OwnerId;

        public static bool IsOwner(this IUser user) => user.Id == OwnerId;

        public static string GetAvatarUrlOrDefault(this IUser user) => user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();

        public static async Task<bool> IsFromPkUserAsync(this IMessage message, IConfigurationRoot config)
        {
            if (message.Author.IsWebhook)
            {
                var gChannel = (message.Channel as IGuildChannel);
                if (gChannel == null)
                    return false;
                var hook = await gChannel.Guild.GetWebhookAsync((message.Author as IWebhookUser).WebhookId);
                return hook.Creator?.Id == ulong.Parse(config["ids:pluralkitid"]);
            }
            return false;
        }

        public static async Task<IUser> GetPkUserAsync(this IMessage message, IConfigurationRoot config)
        {
            var channel = message.Channel as IGuildChannel;
            if (message == null)
                return null;
            var logChannel = await channel.Guild.GetChannelAsync(ulong.Parse(config["ids:pklogchannel"])) as ITextChannel;
            var messages = await logChannel.GetMessagesAsync(message.Id, Direction.After).FlattenAsync();
            var pkId = ulong.Parse(config["ids:pluralkitid"]);
            foreach (var pKMessage in messages)
            {
                if (pKMessage.Author.Id != pkId)
                    continue;
                var idText = pKMessage.Content?.Split('/').Last();
                if (string.IsNullOrWhiteSpace(idText))
                    continue;
                ulong id;
                if (ulong.TryParse(idText, out id) && id == message.Id)
                {
                    try
                    {
                        var footerText = pKMessage.Embeds.FirstOrDefault().Footer.Value.Text;
                        var userIdStr = footerText.Substring(0, footerText.LastIndexOf(')')).Split('(').Last();
                        var userId = ulong.Parse(userIdStr);
                        return await channel.Guild.GetUserAsync(userId);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            return null;
        }
    }
}