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

        public enum ErrorType
        {
            UserError,
            ParsingOrArgError,
            Exception
        }

        public async Task ReportErrorAsync(IUser user, IMessageChannel channel, string commandName, string errorReason, ErrorType errorType, IDiscordInteraction interaction)
        {
            try
            {
                var guild = (channel as IGuildChannel)?.Guild;
                string errorMessage = null;
                var isEphemeral = false;
                var isCommandException = false;

                if (!string.IsNullOrEmpty(errorReason))
                {
                    errorMessage = errorReason;
                    if (errorType == ErrorType.Exception && errorMessage.StartsWith("COMMANDEXCEPTION:"))
                    {
                        errorType = ErrorType.UserError;
                        isCommandException = true;
                        errorMessage = errorMessage.Substring("COMMANDEXCEPTION:".Count());
                    }
                    if (errorMessage.StartsWith("EPHEMERAL:"))
                    {
                        errorMessage = errorMessage.Substring("EPHEMERAL:".Count());
                        isEphemeral = true;
                    }
                }

                errorMessage ??= "";
                errorMessage = errorMessage.Length <= 3000 ? errorMessage : errorMessage.Substring(0, 3000);

                //if (result.Error == CommandError.ParseFailed || result.Error == CommandError.ObjectNotFound || result.Error == CommandError.BadArgCount)
                //    errorMessage = DialogueDict.Get("ERROR_MESSAGE", errorMessage, config.GetDefaultPrefix(), commandName);

                if (errorMessage != null)
                {

                    var finalMessage = errorType == ErrorType.UserError
                        ? errorMessage
                        : (errorType == ErrorType.Exception
                                ? DialogueDict.Get("EXCEPTION", errorMessage)
                                : DialogueDict.Get("ERROR_MESSAGE", errorMessage, config.GetDefaultPrefix(), commandName));
                    if (interaction != null)
                    {
                        if (interaction.HasResponded)
                            await interaction.FollowupAsync(embed:
                                EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(finalMessage), client as DiscordSocketClient).Build(),
                                ephemeral: isEphemeral);
                        else
                            await interaction.RespondAsync(embed:
                                EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(finalMessage), client as DiscordSocketClient).Build(),
                                ephemeral: isEphemeral);
                    }
                    else
                    {
                        await channel.SendMessageAsync(embed:
                            EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(finalMessage), client as DiscordSocketClient).Build());
                    }
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
