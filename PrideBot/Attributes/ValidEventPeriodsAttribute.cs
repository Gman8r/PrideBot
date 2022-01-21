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
            if ((context.User as SocketUser).IsGYNSage(config)
                || (context.User is SocketGuildUser gUser && gUser.GuildPermissions.Administrator))
                return Task.FromResult(PreconditionResult.FromSuccess());
            var month = DateTime.Now.Month;
            var eventStart = DateTime.Parse(config["eventstart"]);
            var eventEnd = DateTime.Parse(config["eventend"]);
            if (DateTime.Now < eventStart && ValidPeriods.HasFlag(EventPeriod.BeforeEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            else if ((DateTime.Now > eventStart && DateTime.Now < eventEnd) && ValidPeriods.HasFlag(EventPeriod.DuringEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());
            if (DateTime.Now > eventStart && ValidPeriods.HasFlag(EventPeriod.AfterEvent))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError(DialogueDict.Get("ERROR_WRONG_TIME")));
        }
    }
}
