using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
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
            var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await services.GetService<ModelRepository>().GetOrCreateUserAsync(connection, context.User.Id.ToString());
            if (!dbUser.ShipsSelected)
                return PreconditionResult.FromError($"You need to be registered for the event to use that command! Sign up with " +
                    $"`{services.GetService<IConfigurationRoot>().GetDefaultPrefix()}register`.");

            return PreconditionResult.FromSuccess();
        }
    }
}
