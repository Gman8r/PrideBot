using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Discord;
using PrideBot.Modules;
using Discord.Commands;

namespace PrideBot
{
    public class CommandErrorReportingService
    {
        private readonly IConfigurationRoot config;
        private readonly LoggingService loggingService;
        private readonly DiscordSocketClient client;

        public CommandErrorReportingService(IConfigurationRoot config, LoggingService loggingService, DiscordSocketClient client)
        {
            this.config = config;
            this.loggingService = loggingService;
            this.client = client;
        }

        public async Task ReportErrorAsync(IUser user, IMessageChannel channel, string commandName, string errorReason, bool isException)
        {
            try
            {
                var guild = (channel as IGuildChannel)?.Guild;
                //if (!(channel is IGuildChannel)
                //    && !(guild as SocketGuild).GetUser(context.Client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel).Has(ChannelPermission.SendMessages))
                //{
                //    // Can't send messages, so just forget it
                //    return;
                //}

                string errorMessage = null;

                //if (result.Error == CommandError.UnknownCommand)
                //{
                //    var argPos = commandArgData[context.Message.Id];
                //    var commandName = context.Message.Content.Substring(argPos).TrimEnd();
                //    var module = commands.Modules
                //        .FirstOrDefault(a => a.IsSubmodule
                //        && (commandName.StartsWith(a.GetModulePathPrefix(), StringComparison.OrdinalIgnoreCase) // ![submodule] [wrongcommand]
                //        || commandName.Equals(a.GetModulePathPrefix().TrimEnd(), StringComparison.OrdinalIgnoreCase))); // ![submodule]
                //    if (module != null)
                //    {
                //        // Submodule help (user used an invalid command in a submodule)
                //        var moduleClass = PrideModuleBase.GetModule(module, commands).Value;

                //        errorMessage = $"Invalid subcommand for {module.GetModulePathPrefix().TrimEnd()}.\n\n" +
                //            await moduleClass.GetHelpLineAsync(module, module.Commands, context as SocketCommandContext, provider, config);
                //    }
                //}
                if (!string.IsNullOrEmpty(errorReason))
                {
                    var commandException = isException && errorReason.StartsWith("COMMANDEXCEPTION:");
                    if (!isException)
                    {
                        errorMessage = errorReason;
                    }
                    else
                    {
                        if (errorReason.StartsWith("COMMANDEXCEPTION:"))
                            errorMessage = (errorReason.Substring("COMMANDEXCEPTION:".Count()));
                        else
                            errorMessage = DialogueDict.Get("EXCEPTION");
                    }
                }

                //if (result.Error == CommandError.ParseFailed || result.Error == CommandError.ObjectNotFound || result.Error == CommandError.BadArgCount)
                //    errorMessage = DialogueDict.Get("ERROR_MESSAGE", errorMessage, config.GetDefaultPrefix(), commandName);

                if (errorMessage != null)
                {
                    await channel.SendMessageAsync(embed:
                        EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(errorMessage), client as DiscordSocketClient).Build());
                }
            }
            catch (Exception e)
            {
                var logMessage = new LogMessage(LogSeverity.Error, "CommandHandler",
                    "Error reporting an error: " + errorReason, e);
                await loggingService.OnLogAsync(logMessage);
            }
        }
    }
}
