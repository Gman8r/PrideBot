using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class LimitInstancesAttribute : PreconditionAttribute
    {
        int PerGuild { get; }
        int PerDM { get; }
        string GroupName { get; }

        public LimitInstancesAttribute(int perGuild, int perDM, string groupName = null)
        {
            PerGuild = perGuild;
            PerDM = perDM;
            GroupName = groupName;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext iContext, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IConfigurationRoot>();
            if (bool.Parse(config["ownerignoresratelimits"]) && iContext.User.IsOwner(config))
                return Task.FromResult(PreconditionResult.FromSuccess());

            //var tsuchiContext = iContext as TsuchiSocketCommandContext;
            //var service = services.GetService(typeof(CommandTrackingService)) as CommandTrackingService;
            //var groupName = (GroupName ?? command.GetFullCommandPath()) + " instances";
            //var isGuild = tsuchiContext.Guild != null;
            //IEnumerable<CommandTrackingService.Command> cooldownData;
            //if (isGuild)
            //    cooldownData = service.ActiveCommands
            //        .Where(a => a.Context.Guild?.Id == tsuchiContext.Guild?.Id
            //        && a.GroupName == groupName
            //        && !a.Finished
            //        && a.Context.Id != tsuchiContext.Id); // To recognize when a command is falling back on another command of the same name
            //else
            //    cooldownData = service.ActiveCommands
            //        .Where(a => a.Context.User.Id == tsuchiContext.User.Id
            //        && a.GroupName == groupName
            //        && !a.Finished
            //        && a.Context.Id != tsuchiContext.Id); // To recognize when a command is falling back on another command of the same name
            //var max = isGuild ? PerGuild : PerDM;

            //if (cooldownData.Count() >= max)
            //{
            //    return Task.FromResult(PreconditionResult.FromError($"I can only run {max} of those commands at a time in a given " +
            //        $"{(isGuild ? "server. Settle down, everyone!" : "DM chat.")}"));
            //}

            //service.CreateCommand(tsuchiContext, groupName);
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
