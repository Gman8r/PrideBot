using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrideBot.Game;
using PrideBot.Registration;
using PrideBot.Repository;
using PrideBot.Sheets;
using PrideBot.Quizzes;

namespace PrideBot
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }
        public static bool DebugMode { get; private set; }

        public Startup(string[] args)
        {
            DebugMode = bool.Parse(File.ReadAllText("debugmode.txt"));
            var configPath = DebugMode ? "configdebug.yml" : "config.yml";
            var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
                .SetBasePath(AppContext.BaseDirectory)      // Specify the default location for the config file
                .AddYamlFile(configPath);                // Add this (yaml encoded) file to the configuration
            Configuration = builder.Build();                // Build the configuration
        }

        public static async Task RunAsync(string[] args)
        {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        public async Task RunAsync()
        {
            if (File.Exists("shutdown"))
            {
                Console.WriteLine("The bot was shut down in an emergency, please make sure all necessary maintenance is done and remove the shutdown file in the program folder before running again.");
                await Task.Delay(-1);
            }

            var services = new ServiceCollection();             // Create a new instance of a service collection
            ConfigureServices(services);

            var provider = services.BuildServiceProvider();     // Build the service provider
            provider.GetRequiredService<LoggingService>();      // Start the logging service
            provider.GetRequiredService<CommandHandler>();      // Start the command handler service
            provider.GetRequiredService<DialogueDict>();

            if (!(bool)Configuration.ParseBoolField("stealthmode")) 
            {
                // PrideBot-specific services
                provider.GetRequiredService<PlayStatusService>();
                provider.GetRequiredService<ScoringService>();
                provider.GetRequiredService<DailyQuizService>();
                provider.GetRequiredService<StarboardScoringService>();
                provider.GetRequiredService<UserRegisteredCache>();
                provider.GetRequiredService<ChatScoringService>();
            }
            else
            {
                await provider.GetService<LoggingService>().OnLogAsync(new LogMessage(LogSeverity.Info, "Stealth Mode",
                    "\n----------------------------------------------------------" +
                    "\nStealh mode active for session!!" +
                    "\n----------------------------------------------------------"));
            }
            var token = File.ReadAllText(DebugMode ? "tokendebug.txt" : "token.txt");
            await provider.GetRequiredService<StartupService>().StartAsync(token);       // Start the startup service
            await Task.Delay(-1);                               // Keep the program alive
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {                                       // Add discord to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                MessageCacheSize = 1000,             // Cache 1,000 messages per channel
                AlwaysDownloadUsers = true
            }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {                                       // Add the command service to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
            }))
            .AddSingleton<CommandHandler>()         // Add the command handler to the collection
            .AddSingleton<StartupService>()         // Add startupservice to the collection
            .AddSingleton<LoggingService>()         // Add loggingservice to the collection
            .AddSingleton<Random>()                 // Add random to the collection
            .AddSingleton<ModelRepository>()
            .AddSingleton<DialogueDict>()
            .AddSingleton(Configuration);             // Add the configuration to the collection

            Console.WriteLine();

            if (!Configuration.ParseBoolField("stealthmode"))
            {
                services.AddSingleton<PlayStatusService>();
                services.AddSingleton<GoogleSheetsService>();
                services.AddSingleton<ShipImageGenerator>();
                services.AddSingleton<ScoringService>();
                services.AddSingleton<DailyQuizService>();
                services.AddSingleton<StarboardScoringService>();
                services.AddSingleton<UserRegisteredCache>();
                services.AddSingleton<ChatScoringService>();
                //.AddSingleton<PlayStatusService>();
            }
        }
    }
}
