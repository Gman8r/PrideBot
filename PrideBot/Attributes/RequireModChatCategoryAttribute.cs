using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class RequireModChatCategoryAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.Guild != null)
            {
                var config = services.GetService<IConfigurationRoot>();
                var modChat = (context.Guild as SocketGuild).GetChannel(ulong.Parse(config["ids:modchat"])) as SocketTextChannel;
                if (modChat != null && modChat.Category == (context.Channel as SocketTextChannel).Category)
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError("That command can only be used in the same channel category as GYN mod chat."));
        }
    }
}
