using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PrideBot.Modules;
using PrideBot.TypeReaders;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace PrideBot
{
    public class StartupService
    {
        private readonly IServiceProvider provider;
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;

        // DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config)
        {
            this.provider = provider;
            this.config = config;
            this.discord = discord;
            this.commands = commands;
        }

        public async Task StartAsync(string discordToken)
        {
            if (string.IsNullOrWhiteSpace(discordToken))
                throw new CommandException("No token found in text file.");

            await discord.LoginAsync(TokenType.Bot, discordToken);     // Login to discord
            await discord.StartAsync();                                // Connect to the websocket

            // Add type readers
            commands.AddTypeReader(typeof(IEmote), new IEmoteTypeReader<IEmote>());
            commands.AddTypeReader(typeof(Emote), new IEmoteTypeReader<Emote>());
            commands.AddTypeReader(typeof(Emoji), new IEmoteTypeReader<Emoji>());
            commands.AddTypeReader(typeof(MessageUrl), new UrlMessageTypeReader());

            if (!config.ParseBoolField("stealthmode"))
                await commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);     // Load commands and modules into the command service
            else
                await commands.AddModuleAsync<SecretStealthModule>(provider);  // Only load stealth module modules

            // Initialize helper fields
            var appData = await discord.GetApplicationInfoAsync();
            UserHelper.OwnerId = appData.Owner.Id;
        }
    }
}
