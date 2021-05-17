using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Newtonsoft.Json;

namespace PrideBot.Modules
{
    [Name("General")]
    public class GeneralModule : PrideModuleBase
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;

        public override int HelpSortOrder => -100;

        public GeneralModule(CommandService service, IConfigurationRoot config, IServiceProvider provider)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
        }

        [Command("help")]
        [Alias("help!")]
        [Summary("You did it!")]
        public async Task Help([Remainder] string command = "")
        {
            await ReplyAsync(embed: (await GenerateHelpMessageAsync(command, Context, service, config, provider)).Build());
        }

        public static async Task<EmbedBuilder> GenerateHelpMessageAsync(string command, SocketCommandContext Context, CommandService service, IConfigurationRoot config, IServiceProvider provider)
        {
            var moduleDict = GetModuleClassDictionary(service)
               .OrderBy(a => a.Value.HelpSortOrder)
               .ToDictionary(t => t.Key, t => t.Value);

            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithTitle("Gensokyo Pride Games Help");


            if (string.IsNullOrEmpty(command))
            {
                // General help

                var prefixes = config.GetPrefixes();
                var defaultPrefix = config.GetDefaultPrefix();
                var helpMessage = DialogueDict.Get("HELP", defaultPrefix)
                + "\n\n";
                //+ "Use any of these prefixes (case-insensitive) or mention me to call me:\n"
                //+ string.Join("    ", ConfigHelper.GetPrefixes(config).Select(a => $"`{a}{{command}}`"))

                embed.Description = helpMessage;

                var allUsableCommands = service.Commands
                    .Where(a => !a.Module.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));

                foreach (var module in moduleDict.Where(a => !a.Key.IsSubmodule))
                {
                    var helpLine = await module.Value.GetHelpLineAsync(
                        module.Key, allUsableCommands, Context, provider, config);
                    if (!string.IsNullOrWhiteSpace(helpLine?.Value?.ToString()))
                        embed.AddField(helpLine);
                }
                return embed;
            }
            else
            {
                var matchingSubmodule = service.Modules
                    .FirstOrDefault(a => a.IsSubmodule && a.Aliases.Select(aa => aa.ToLower()).Contains(command.ToLower()));
                if (matchingSubmodule != null && (await matchingSubmodule.GetExecutableCommandsAsync(Context, provider)).Any())
                {
                    // Submodule help

                    var moduleData = GetModule(matchingSubmodule, service);
                    var commands = await matchingSubmodule.GetExecutableCommandsAsync(Context, provider);
                    embed = embed
                        .WithTitle(matchingSubmodule.Name + " Help")
                        .WithDescription(matchingSubmodule.Summary ?? "Hella.");
                    embed.AddField(await moduleData.Value.GetHelpLineAsync(moduleData.Key, commands, Context, provider, config));
                    return embed;
                }
                else
                {
                    // Command help

                    var result = GetCommandHelp(command, Context, service, provider, config);
                    if (result.IsSuccess)
                    {
                        embed = embed
                            .WithTitle("Command Help")
                            .WithDescription(result.Value);
                        return embed;
                    }
                    else
                    {
                        return EmbedHelper.GetEventErrorEmbed(Context.User, result.ErrorMessage, Context.Client);
                    }
                }
            }
        }

        public static ValueResult<string> GetCommandHelp(string commandName, SocketCommandContext context, CommandService service, IServiceProvider provider, IConfigurationRoot config)
        {
            var result = service.Search(commandName);
            var errorStr = DialogueDict.Get("COMMAND_NOT_FOUND", commandName);
            if (!result.IsSuccess)
                return ValueResult<string>.Error(errorStr);
            var matches = result.Commands
                .Where(a => !a.Command.Module.Name.Contains("secret", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (!matches.Any())
                return ValueResult<string>.Error(errorStr);

            var message = "";
            foreach (var match in matches)
            {
                var command = match.Command;
                var fullName = $"{(command.Module.IsSubmodule ? command.Module.Name + " " : "")}{command.Name}";
                if (fullName.Trim().Split().Length < commandName.Trim().Split().Length) // Only take matches from the same submodule depth as input text
                    continue;

                var parameterStrings = new List<string>();
                foreach (var parameter in command.Parameters)
                {
                    var displayName = parameter.Attributes.OfType<NameAttribute>().Any()
                        ? parameter.Name
                        : StringHelper.CamelCaseSpaces(parameter.Name);
                    string paramStr;
                    if (parameter.IsOptional)
                    {
                        var defaultValue = parameter.Attributes.OfType<DefaultValueNameAttribute>().FirstOrDefault()?.Text
                            ?? parameter.DefaultValue?.ToString()
                            ?? "";
                        if (string.IsNullOrWhiteSpace(defaultValue))
                            paramStr = $"<{displayName}>";
                        else
                            paramStr = $"<{displayName} ({defaultValue})>";
                    }
                    else
                    {
                        paramStr = $"[{displayName}]";
                    }
                    if (parameter.IsMultiple)
                        paramStr += "...";
                    parameterStrings.Add(paramStr);
                }

                message += $"\n\n**Usage:**  {ConfigHelper.GetPrefixes(config)[0]}{fullName}";
                if (parameterStrings.Count() > 0)
                    message += " `" + string.Join("` `", parameterStrings) + "`";

                var aliases = command.Aliases.Where(a => !a.Contains('!') && !a.Equals(fullName, StringComparison.OrdinalIgnoreCase));
                if (aliases.Any())
                    message += $"\n**Aliases:**  {string.Join(", ", aliases)}";

                if (!string.IsNullOrWhiteSpace(command.Summary))
                    message += "\n**Description:**  " + command.Summary;
            }

            if (string.IsNullOrWhiteSpace(message))
                return ValueResult<string>.Error($"Command {commandName} not found");

            return ValueResult<string>.Success(message);
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(0)]
        [Summary("Relays a message.")]
        public async Task Echo([Remainder] string message)
        {
            await ReplyAsync(DialogueDict.RollBullshit(message));
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(1)]
        [Summary("Relays a message in the specified chat channel.")]
        public async Task Echo(SocketTextChannel channel, [Remainder] string message)
        {
            await channel.SendMessageAsync(DialogueDict.RollBullshit(message));
            await ReplyResultAsync("Done.");
        }

    }
}