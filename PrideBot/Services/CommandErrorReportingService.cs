﻿using Discord.WebSocket;
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

        public async Task ReportErrorAsync(IUser user, IMessageChannel channel, string commandName, string errorReason, bool isException, IDiscordInteraction interaction)
        {
            try
            {
                var guild = (channel as IGuildChannel)?.Guild;
                string errorMessage = null;
                var ephemeral = false;

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
                        {
                            errorMessage = (errorReason.Substring("COMMANDEXCEPTION:".Count()));
                        }
                        else
                            errorMessage = errorReason.Length <= 4000 ? errorMessage : errorReason.Substring(0, 4000);
                        //errorMessage = DialogueDict.Get("EXCEPTION");
                    }
                    if (errorMessage.StartsWith("EPHEMERAL:"))
                    {
                        errorMessage = (errorMessage.Substring("EPHEMERAL:".Count()));
                        ephemeral = true;
                    }
                }

                //if (result.Error == CommandError.ParseFailed || result.Error == CommandError.ObjectNotFound || result.Error == CommandError.BadArgCount)
                //    errorMessage = DialogueDict.Get("ERROR_MESSAGE", errorMessage, config.GetDefaultPrefix(), commandName);

                if (errorMessage != null)
                {
                    if (interaction != null)
                    {
                        if (interaction.HasResponded)
                            await interaction.FollowupAsync(embed:
                                EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(errorMessage), client as DiscordSocketClient).Build(),
                                ephemeral: ephemeral);
                        else
                            await interaction.RespondAsync(embed:
                                EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(errorMessage), client as DiscordSocketClient).Build(),
                                ephemeral: ephemeral);
                    }
                    else
                    {
                        await channel.SendMessageAsync(embed:
                            EmbedHelper.GetEventErrorEmbed(user, DialogueDict.GenerateEmojiText(errorMessage), client as DiscordSocketClient).Build());
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
