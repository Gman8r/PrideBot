using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    class RequireSageAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IConfigurationRoot>();
            if ((context.User as SocketUser).IsGYNSage(config))
                return Task.FromResult(PreconditionResult.FromSuccess());
            //else if (context.User is SocketGuildUser gUser && gUser.GuildPermissions.Has(Discord.GuildPermission.Administrator))
            //    return Task.FromResult(PreconditionResult.FromSuccess());
            //else
                return Task.FromResult(PreconditionResult.FromError("You need to be one of GYN's sages use that command."));
        }
    }
}
