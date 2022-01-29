using Discord.Commands;
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
    public class RequireSingleSessionAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var existingSession = Session.activeSessions.FirstOrDefault(a => a.GetUser().Id == context.User.Id);
            if (existingSession != null)
            {
                var messageUrl = existingSession.GetCurrentPromptMessage().GetJumpUrl() ?? "";
                var errorStr = !string.IsNullOrWhiteSpace(messageUrl)
                    ? DialogueDict.Get("SESSION_DUPE_LINK", messageUrl)
                    : DialogueDict.Get("SESSION_DUPE");
                return PreconditionResult.FromError("EPHEMERAL:" + errorStr);
            }

            return PreconditionResult.FromSuccess();
        }
    }
}
