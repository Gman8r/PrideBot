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
    public static class GuildHelper
    {

        public static bool IsInviteUser(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var patterns = new string[] {
                "discord.gg/",
                "discordapp.com/invite/" };
            return patterns
                .Where(a => name.Contains(a))
                .Any();
        }
        public static bool IsInviteUser(IUser user, string nickname)
        {
            return IsInviteUser(user.Username) || IsInviteUser(nickname);
        }

        public static bool IsServerChannel(this ISocketMessageChannel channel)
            => (channel.GetType() == typeof(SocketTextChannel)
            && ((SocketTextChannel)channel).Guild != null);

        public static string GetAvatarUrlDownloadable(this IUser user) => user.GetAvatarUrl().Split('?')[0];

        public static SocketRole GetModRole(this SocketGuild guild)
            => guild.Roles
                .OrderByDescending(a => a.Position)
                .FirstOrDefault(a => a.Permissions.Has(GuildPermission.Administrator)
                    && a.Members.Any(aa => !aa.IsBot)); // Needs to have at least one non-bot member

        public static SocketRole GetBotRole(this SocketGuild guild, IUser bot)
            => guild.Roles.FirstOrDefault(a => a.Tags?.BotId == bot.Id);
    }
}
