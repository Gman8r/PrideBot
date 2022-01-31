using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Registration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class RequireRegistrationAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            using var connection = services.GetService<ModelRepository>().GetDatabaseConnection();
            var cache = services.GetService<UserRegisteredCache>();
            if (await cache.GetOrDownloadAsync(context.User.Id.ToString()))
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError($"You need to be registered for the event to use that command! Sign up with " +
                        $"`{services.GetService<IConfigurationRoot>().GetDefaultPrefix()}register`.");
        }
    }
}
