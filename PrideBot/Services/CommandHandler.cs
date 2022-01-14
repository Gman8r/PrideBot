using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Discord;

namespace PrideBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly LoggingService loggingService;
        private readonly CommandErrorReportingService errorReportingService;

        private Dictionary<ulong, int> commandArgData;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(
            DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider, LoggingService loggingService, CommandErrorReportingService errorReportingService)
        {
            this.client = client;
            this.commands = commands;
            this.config = config;
            this.provider = provider;
            commandArgData = new Dictionary<ulong, int>();

            client.MessageReceived += OnMessageReceivedAsync;
            commands.CommandExecuted += OnCommandExecutedAsync;
            this.loggingService = loggingService;
            this.errorReportingService = errorReportingService;
        }

        private async Task OnMessageReceivedAsync(SocketMessage s)
        {
            var msg = s as SocketUserMessage;     // Ensure the message is from a user/bot
            if (msg == null) return;
            if (msg.Author.IsBot) return;     // Ignore bots when checking commands

            var context = new SocketCommandContext(client, msg);     // Create the command context

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasPrefix(config, ref argPos) || msg.HasMentionPrefix(client.CurrentUser, ref argPos))
            {
                commandArgData[msg.Id] = argPos;
                await commands.ExecuteAsync(context, argPos, provider);     // Execute the command
            }
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!string.IsNullOrEmpty(result?.ErrorReason) && result.Error != CommandError.UnknownCommand)
                await errorReportingService.ReportErrorAsync(context.User, context.Channel, (command.IsSpecified && command.Value.Name != null) ? command.Value.Name : "",
                    result.ErrorReason, result.Error == CommandError.Exception);
            commandArgData.Remove(context.Message.Id);
        }
    }
}
