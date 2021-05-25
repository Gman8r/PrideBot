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
using PrideBot.Repository;

namespace PrideBot.Modules
{
    [Name("General")]
    public class GeneralModule : PrideModuleBase
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;

        public override int HelpSortOrder => -100;

        public GeneralModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
        }

        [Command("help")]
        [Alias("help!")]
        [Summary("Yassss you like, Nailed using that help command by using it on the help command! Nice goiiiinggg!")]
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
                var prefixStr = string.Join(", ", prefixes.Select(a => $"`{a}{{command}}`"));
                var helpMessage = DialogueDict.Get("HELP", defaultPrefix, prefixStr)
                + "\n\n";
                //+ "Use any of these prefixes (case-insensitive) or mention me to call me:\n"
                //+ string.Join("    ", ConfigHelper.GetPrefixes(config).Select(a => $"`{a}{{command}}`"))

                embed.Description = helpMessage;

                var allUsableCommands = service.Commands
                    .Where(a => !a.Module.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                for (int i = allUsableCommands.Count - 1; i >= 0; i--)
                {
                    if (!(await UserHasPermissionsForCommand(Context, allUsableCommands[i], provider)))
                        allUsableCommands.RemoveAt(i);
                }

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
                    message += "\n**Description:**  " + DialogueDict.RollBullshit(command.Summary);
            }

            if (string.IsNullOrWhiteSpace(message))
                return ValueResult<string>.Error($"Command {commandName} not found");

            return ValueResult<string>.Success(message);
        }

        static async Task<bool> UserHasPermissionsForCommand(SocketCommandContext context, CommandInfo command, IServiceProvider provider)
        {
            return (await command.CheckPreconditionsAsync(context, provider)).IsSuccess;

            //var sageAttribute = command.Preconditions.FirstOrDefault(a => a.GetType() == typeof(RequireSageAttribute))
            //    ?? command.Module.Preconditions.FirstOrDefault(a => a.GetType() == typeof(RequireSageAttribute));
            //if (sageAttribute == null) return true;
            //var result = await (sageAttribute as RequireSageAttribute).CheckPermissionsAsync(context, command, provider);
            //return result.IsSuccess;
        }

        [Command("setpings")]
        [Summary("Use with True or False to change whether I ping you for achievements.")]
        public async Task SetPings(bool ping)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var dbUser = await repo.GetOrCreateUserAsync(connection, Context.User.Id.ToString());
            if (dbUser.PingForAchievements != ping)
            {
                dbUser.PingForAchievements = ping;
                await repo.UpdateUserAsync(connection, dbUser);
            }
            if (ping)
                await ReplyAsync("Done! I'll be pinging you for your achievements, 'kay?");
            else
                await ReplyAsync("Done! I will no longer ping you when you get an achievement.");
        }

    }
}