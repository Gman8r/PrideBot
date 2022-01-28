using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace PrideBot
{
    public class RequireSingleSessionInteractionAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            if (Session.activeSessions.Any(a => a.GetUser().Id == context.User.Id))
                return PreconditionResult.FromError("EPHEMERAL:" + DialogueDict.Get("SESSION_DUPE"));

            return PreconditionResult.FromSuccess();
        }
    }
}
