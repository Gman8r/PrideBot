using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace PrideBot
{
    class RequireSageAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IConfigurationRoot>();
            if ((context.User as SocketUser).IsGYNSage(config))
                return PreconditionResult.FromSuccess();
            else if (context.User is SocketGuildUser gUser && gUser.GuildPermissions.Has(Discord.GuildPermission.Administrator))
                return PreconditionResult.FromSuccess();
            else if ((await File.ReadAllLinesAsync("gods.txt")).Contains(context.User.Id.ToString()))
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError("You need to be one of GYN's sages use that command.");
        }
    }
}
