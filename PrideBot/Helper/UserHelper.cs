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

namespace PrideBot
{
    static class UserHelper
    {
        public static ulong OwnerId;

        public static bool IsOwner(this IUser user) => user.Id == OwnerId;

        public static string GetAvatarUrlOrDefault(this IUser user) => user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
    }
}
