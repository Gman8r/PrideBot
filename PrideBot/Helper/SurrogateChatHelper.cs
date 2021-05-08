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
    public static class SurrogateChatHelper
    {

        public static async Task<IWebhook> CreateUserWebhookAsync(SocketTextChannel channel, SocketGuildUser user, IConfigurationRoot config)
        {
            var avatarFile = await new FileDownloader(config.GetRelativeFilePath("temp"))
                .DownloadFileAsync(user.GetAvatarUrl());
            Stream imageStream = new FileStream(avatarFile, FileMode.Open);
            return await channel.CreateWebhookAsync(user.Username, imageStream);
        }
    }
}
