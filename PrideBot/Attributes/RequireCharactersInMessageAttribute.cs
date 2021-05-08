using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    class RequireCharactersInMessageAttribute : PreconditionAttribute
    {

        public char[] Chars { get; }

        public RequireCharactersInMessageAttribute(params char[] chars)
        {
            Chars = chars;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (Chars.Except(context.Message.Content).Any())
                return Task.FromResult(PreconditionResult.FromError("That command needs specific characters in the message to use. Also, you shouldn't be seeing this message!"));
            else
                return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
