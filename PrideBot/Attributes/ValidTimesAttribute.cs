using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class ValidTimesAttribute : PreconditionAttribute
    {

        readonly Times ValidTimes;

        public ValidTimesAttribute(Times validTimes)
        {
            ValidTimes = validTimes;
        }

        [Flags]
        public enum Times
        {
            BeforeEvent,
            DuringEvent,
            AfterEvent
        }


        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetService<IConfigurationRoot>();
            if ((context.User as SocketUser).IsGYNSage(config))
                return Task.FromResult(PreconditionResult.FromSuccess());
            var month = DateTime.Now.Month;
            var eventMonth = int.Parse(config["eventmonth"]);
            if (month < eventMonth && ValidTimes.HasFlag(Times.BeforeEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            else if (month == eventMonth && ValidTimes.HasFlag(Times.DuringEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            else if (month > eventMonth && ValidTimes.HasFlag(Times.AfterEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError("Hmmm huhhh hold up, it looks like you aren't able to use this command at this time. Contact a sage if you think this something's wrong, 'kayyy?"));
        }
    }
}
