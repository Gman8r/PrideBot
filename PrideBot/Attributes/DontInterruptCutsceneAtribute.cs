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
using PrideBot.Events;

namespace PrideBot
{
    public class DontInterruptCutsceneAtribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var service = services.GetService<SceneDialogueService>();
            if (service.IsMidScene)
            {
                return PreconditionResult.FromError(DialogueDict.Get("INTERRUPTED_MONOLOGUE", $"<#{service.ActiveSceneChannel.Id}>"));
            }

            return PreconditionResult.FromSuccess();
        }
    }
}
