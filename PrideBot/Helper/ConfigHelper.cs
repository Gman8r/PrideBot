using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace PrideBot
{
    static class ConfigHelper
    {
        public static bool ParseBoolField(this IConfigurationRoot config, string key) => bool.Parse(config[key]);

        public static ulong ParseUlongField(this IConfigurationRoot config, string key) => ulong.Parse(config[key]);

        public static bool IsOwner(this IUser user, IConfigurationRoot config)
            => user.Id == config.ParseUlongField("ids:owner");

        public static bool IsGYNSage(this SocketUser user, IConfigurationRoot config)
        {
            if (user.IsOwner(config))
                return true;
            var gyn = user.MutualGuilds.FirstOrDefault(a => a.Id == config.ParseUlongField("ids:gyn"));
            if (gyn == null)
                return false;
            var sage = gyn.GetRole(config.ParseUlongField("ids:sage"));
            return sage.Members.Any(a => a.Id == user.Id);
        }

        public static string[] GetPrefixes(this IConfigurationRoot config)
            => config.GetSection("prefixes").GetChildren().Select(a => a.Value).ToArray();

        public static string GetDefaultPrefix(this IConfigurationRoot config)
            => GetPrefixes(config)[0];

        public static string GetTopLevelDirectory(this IConfigurationRoot config) => config["paths:toplevel"];

        public static string GetRelativeFilePath(this IConfigurationRoot config, string path)
            => Path.Combine(GetTopLevelDirectory(config), path);

        public static string GetRelativeHostPathWeb(this IConfigurationRoot config, string path)
            => config["paths:wwwhostpath"] + path;

        public static string GetRelativeHostPathLocal(this IConfigurationRoot config, string path)
            => Path.Combine(config["paths:wwwhostpathlocal"], path);

        public static string GetGuildDirectory(this IConfigurationRoot config, IGuild guild)
            => GetRelativeFilePath(config, config["paths:guilds"] + "/" + guild.Id);

        public static string GetRelativeFilePathForGuild(this IConfigurationRoot config, IGuild guild, string path)
            => Path.Combine(GetGuildDirectory(config, guild), path);

        static string GetUserDirectory(this IConfigurationRoot config, IUser user)
            => GetRelativeFilePath(config, config["paths:users"] + "/" + user.Id);

        public static bool UserDirectoryExists(this IConfigurationRoot config, IUser user)
            => Directory.Exists(GetUserDirectory(config, user));

        public static string GetOrCreateUserDirectory(this IConfigurationRoot config, IUser user)
        {
            var path = GetUserDirectory(config, user);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                File.Create(Path.Combine(path, $"{user.Username}#{user.Discriminator}"));
            }
            return path;
        }

        public static string GetRelativeFilePathForUser(this IConfigurationRoot config, IUser user, string path)
            => Path.Combine(GetUserDirectory(config, user), path);

    }
}
