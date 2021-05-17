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

        private Dictionary<ulong, int> commandArgData;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(
            DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider, LoggingService loggingService)
        {
            this.client = client;
            this.commands = commands;
            this.config = config;
            this.provider = provider;
            commandArgData = new Dictionary<ulong, int>();

            client.MessageReceived += OnMessageReceivedAsync;
            commands.CommandExecuted += OnCommandExecutedAsync;
            this.loggingService = loggingService;
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
            if (!string.IsNullOrEmpty(result?.ErrorReason))
                await ReportErrorAsync(context, result);
            commandArgData.Remove(context.Message.Id);
        }

        public async Task ReportErrorAsync(ICommandContext context, IResult result)
        {
            try
            {
                if (context.Guild != null
                    && !(context.Guild as SocketGuild).GetUser(context.Client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel).Has(ChannelPermission.SendMessages))
                {
                    // Can't send messages, so just forget it
                    return;
                }

                string errorMessage = null;

                if (result.Error == CommandError.UnknownCommand)
                {
                    var argPos = commandArgData[context.Message.Id];
                    var commandName = context.Message.Content.Substring(argPos).TrimEnd();
                    var module = commands.Modules
                        .FirstOrDefault(a => a.IsSubmodule
                        && (commandName.StartsWith(a.GetModulePathPrefix(), StringComparison.OrdinalIgnoreCase) // ![submodule] [wrongcommand]
                        || commandName.Equals(a.GetModulePathPrefix().TrimEnd(), StringComparison.OrdinalIgnoreCase))); // ![submodule]
                    if (module != null)
                    {
                        // Submodule help (user used an invalid command in a submodule)
                        var moduleClass = PrideModuleBase.GetModule(module, commands).Value;

                        errorMessage = $"Invalid subcommand for {module.GetModulePathPrefix().TrimEnd()}.\n\n" +
                            await moduleClass.GetHelpLineAsync(module, module.Commands, context as SocketCommandContext, provider, config);
                    }
                }
                else if (!string.IsNullOrEmpty(result.ErrorReason))
                {
                    var commandException = result.Error == CommandError.Exception && result.ErrorReason.StartsWith("COMMANDEXCEPTION:");
                    if (result.Error != CommandError.Exception)
                    {
                        errorMessage = result.ErrorReason;
                    }
                    else
                    {
                        if (result.ErrorReason.StartsWith("COMMANDEXCEPTION:"))
                            errorMessage = (result.ErrorReason.Substring("COMMANDEXCEPTION:".Count()));
                        else
                            errorMessage = DialogueDict.Get("EXCEPTION");
                    }
                }

                if (errorMessage != null)
                {
                    await context.Channel.SendMessageAsync(embed:
                        EmbedHelper.GetEventErrorEmbed(context.User, DialogueDict.RollBullshit(errorMessage), context.Client as DiscordSocketClient).Build());
                }
            }
            catch (Exception e)
            {
                var logMessasge = new LogMessage(LogSeverity.Error, "CommandHandler",
                    "Error reporting an error: " + result.Error.ToString(), e);
                await loggingService.OnLogAsync(logMessasge);
            }
        }
    }
}
