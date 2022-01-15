using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace PrideBot
{
    public class RequireSingleSessionAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (Session.activeSessions.Any(a => a.GetUser().Id == context.User.Id))
                return PreconditionResult.FromError(DialogueDict.Get("SESSION_DUPE"));

            return PreconditionResult.FromSuccess();
        }
    }
}
