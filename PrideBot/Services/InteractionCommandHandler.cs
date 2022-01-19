using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Discord;

namespace PrideBot
{
    public class InteractionCommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly InteractionService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly LoggingService loggingService;
        private readonly CommandErrorReportingService errorReportingService;

        private Dictionary<ulong, int> commandArgData;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public InteractionCommandHandler(
            DiscordSocketClient client,
            InteractionService service,
            IConfigurationRoot config,
            IServiceProvider provider, LoggingService loggingService, CommandErrorReportingService errorReportingService)
        {
            this.client = client;
            this.service = service;
            this.config = config;
            this.provider = provider;
            commandArgData = new Dictionary<ulong, int>();

            client.InteractionCreated += InteractionCreated;
            service.ComponentCommandExecuted += ComponentCommandExecuted;
            //service.CommandExecuted += OnCommandExecutedAsync;
            this.loggingService = loggingService;
            this.errorReportingService = errorReportingService;
        }

        private Task InteractionCreated(SocketInteraction interaction)
        {
            Task.Run(async () =>
            {
                var context = new InteractionContext(client, interaction, interaction.User, interaction.Channel);
                await service.ExecuteCommandAsync(context, provider);
            });
            return Task.CompletedTask;
        }


        private Task ComponentCommandExecuted(ComponentCommandInfo command, IInteractionContext context, IResult result)
        {
            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(result?.ErrorReason) && result.Error != InteractionCommandError.UnknownCommand)
                    await errorReportingService.ReportErrorAsync(context.User, context.Channel, command?.Name ?? "",
                        result.ErrorReason, result.Error == InteractionCommandError.Exception, context.Interaction);
            });
            return Task.CompletedTask;
        }
    }
}
