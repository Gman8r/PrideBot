using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PrideBot.Game;

namespace PrideBot
{
    public class ValidEventPeriodsAttribute : PreconditionAttribute
    {

        readonly EventPeriod ValidPeriods;

        public ValidEventPeriodsAttribute(EventPeriod validTimes)
        {
            ValidPeriods = validTimes;
        }


        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IConfigurationRoot>();
            if ((context.User as SocketUser).IsGYNSage(config))
                return Task.FromResult(PreconditionResult.FromSuccess());
            var month = DateTime.Now.Month;
            var eventMonth = int.Parse(config["eventmonth"]);
            if (month < eventMonth && ValidPeriods.HasFlag(EventPeriod.BeforeEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            else if (month == eventMonth && ValidPeriods.HasFlag(EventPeriod.DuringEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            else if (month > eventMonth && ValidPeriods.HasFlag(EventPeriod.AfterEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError("Hmmm huhhh hold up, it looks like you aren't able to use this command at this time. Contact a sage if you think this something's wrong, 'kayyy?"));
        }
    }
}
