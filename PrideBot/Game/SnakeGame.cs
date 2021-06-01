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
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Quizzes;
using PrideBot.Game;
using PrideBot.Registration;

namespace PrideBot.Quizzes
{
    class SnakeGame
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        DiscordSocketClient tsuchiClient;
        readonly DiscordSocketClient mainClient;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly UserRegisteredCache userReg;
        readonly TokenConfig tokenConfig;
        readonly IServiceProvider provider;

        SocketVoiceChannel tsuchiConnectedChannel;
        DateTime nextSnakeTime;

        public SnakeGame(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, UserRegisteredCache userReg, TokenConfig tokenConfig, IServiceProvider provider)
        {
            this.repo = repo;
            this.config = config;
            this.mainClient = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.userReg = userReg;
            this.tokenConfig = tokenConfig;

            client.Ready += DoGameLoop;
            client.UserVoiceStateUpdated += UserVoiceStateUpdated;
            this.provider = provider;
        }

        //private Task MainClientReady()
        //{
        //    Task.Run(async () =>
        //    {
        //        tsuchiClient = new DiscordSocketClient();
        //        await tsuchiClient.LoginAsync(TokenType.Bot, tokenConfig["tsuchitoken"]);
        //        await tsuchiClient.StartAsync();

        //        tsuchiClient.Ready += DoGameLoop;
        //        mainClient.UserVoiceStateUpdated += UserVoiceStateUpdated;
        //    }).GetAwaiter();
        //    return Task.CompletedTask;
        //}

        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if (tsuchiConnectedChannel == null || user.IsBot || after.VoiceChannel.Id != tsuchiConnectedChannel.Id)
                return Task.CompletedTask;
            tsuchiConnectedChannel = null;
            Task.Run(async () =>
            {
                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                await scoringService.AddAndDisplayAchievementAsync(connection, user, "SNAKE", tsuchiClient.CurrentUser);
                loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "SnakeGame", $"Snake caught by {user.Username}")).GetAwaiter();
            }).GetAwaiter();
            return Task.CompletedTask;
        }

        private async Task PerformTsuchiTasks(Random rand)
        {
            tsuchiClient = new DiscordSocketClient();
            await tsuchiClient.LoginAsync(TokenType.Bot, tokenConfig["tsuchitoken"]);
            await tsuchiClient.StartAsync();

            var ready = false;
            tsuchiClient.Ready += () => { ready = true; return Task.CompletedTask; };
            while (!ready)
            {
                await Task.Delay(100);
            }

            var tsuchiChannels = tsuchiClient.GetGyn(config).VoiceChannels
                .Where(a => !a.Users.Any())
                .ToArray();

            SocketVoiceChannel channel = null;
            if (tsuchiChannels.Any())
            {
                tsuchiConnectedChannel = tsuchiChannels[rand.Next() % tsuchiChannels.Length];
                channel = tsuchiConnectedChannel;
                int tries = 0;
                while (tries < 5)
                {
                    tries++;
                    try
                    {
                        await tsuchiConnectedChannel.ConnectAsync();
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed try #{tries} to connect Tsuchi to voice.");
                        if (tries == 5)
                            Console.WriteLine(e);
                    }
                }
            }

            while (tsuchiConnectedChannel != null)
            {
                await Task.Delay(100);
            }
            loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "SnakeGame", $"Disconnecting snake.")).GetAwaiter();
            if (channel != null)
                await channel.DisconnectAsync();
            await tsuchiClient.LogoutAsync();
        }

        Task DoGameLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    var rand = new Random();
                    while(!GameHelper.IsEventOccuring(config))
                    {
                        await Task.Delay(60000);
                    }
                    var nextSnakeTime = GetSnakeTime((await GetLastSnakeDayAsync()) == DateTime.Now.Day ? DateTime.Now.Day + 1 : DateTime.Now.Day,
                        await GetVoiceMinutesAsync(), rand);

                    if (nextSnakeTime <= DateTime.Now)
                    {
                        nextSnakeTime = GetSnakeTime(DateTime.Now.Day + 1,
                            await GetVoiceMinutesAsync(), rand);
                    }

                    while (true)
                    {
                        while (DateTime.Now < nextSnakeTime)
                        {
                            await Task.Delay(6000);
                        }

                        if (!GameHelper.IsEventOccuring(config))
                            break;

                        PerformTsuchiTasks(rand).GetAwaiter();
                        mainClient.GetUser(ulong.Parse(config["ids:owner"])).AttemptSendDMAsync(provider, $"Tsuchi in da house.").GetAwaiter();

                        var endTime = nextSnakeTime.AddMinutes(await GetVoiceMinutesAsync());
                        loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "SnakeGame", $"Unleashed snake for " +
                            $"{(endTime - nextSnakeTime).TotalMinutes} minutes (at {endTime}).")).GetAwaiter();
                        while (DateTime.Now < endTime && tsuchiConnectedChannel != null)
                        {
                            await Task.Delay(100);
                        }
                        tsuchiConnectedChannel = null;

                        if (DateTime.Now >= endTime)
                        {
                            loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "SnakeGame", $"Snake not caught today.")).GetAwaiter();
                            await AddSnakeMinutes(5);
                        }
                        else
                            await SetSnakeMinutes(5);

                        await SetLastSnakeDayAsync(DateTime.Now.Day);
                        nextSnakeTime = GetSnakeTime(DateTime.Now.Day + 1,
                            await GetVoiceMinutesAsync(), rand);
                    }
                }
                catch (Exception e)
                {
                    await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                    var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), mainClient, showUser: false)
                        .WithTitle($"Exception in {this.GetType().Name} Module");
                    var modChannel = mainClient.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                    await modChannel.SendMessageAsync(embed: embed.Build());
                    throw e;
                }
                finally
                {
                    if (GameHelper.IsEventOccuring(config))
                    {
                        var modChannel = mainClient.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                        await modChannel.SendMessageAsync("The module will be attempt to re-enable in 30 minutes.");
                        await Task.Delay(30 * 60000);
                        DoGameLoop().GetAwaiter();
                    }
                }
            }).GetAwaiter();
            return Task.CompletedTask;
        }

        public async Task<int> GetLastSnakeDayAsync()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            return (await repo.GetGynGuildSettings(connection, config)).LastSnakeDay;
        }

        public async Task SetSnakeMinutes(int minutes)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var settings = await repo.GetGynGuildSettings(connection, config);
            settings.SnakeMinutes = minutes;
            await repo.UpdateGuildSettingsAsync(connection, settings);
        }

        public async Task AddSnakeMinutes(int minutes)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var settings = await repo.GetGynGuildSettings(connection, config);
            settings.SnakeMinutes += minutes;
            await repo.UpdateGuildSettingsAsync(connection, settings);
        }

        public async Task SetLastSnakeDayAsync(int day)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var settings = await repo.GetGynGuildSettings(connection, config);
            settings.LastSnakeDay = day;
            await repo.UpdateGuildSettingsAsync(connection, settings);
        }

        public async Task<int> GetVoiceMinutesAsync()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            return (await repo.GetGynGuildSettings(connection, config)).SnakeMinutes;
        }

        DateTime GetSnakeTime(int day, int minutesOnChat, Random rand)
        {
            var currentMinute = DateTime.Now.Day == day
                ? (DateTime.Now.Hour * 60) + DateTime.Now.Minute
                : 0;
            var minuteMax = (24 * 60) - (minutesOnChat + 10);
            if (currentMinute >= minuteMax) 
                return DateTime.Now;
            var minuteChosen = currentMinute + (rand.Next() % (minuteMax - currentMinute));
            var dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(day - 1).AddMinutes(minuteChosen);
            //mainClient.GetUser(ulong.Parse(config["ids:owner"])).AttemptSendDMAsync(provider, $"Scheduled snake in {mainClient.GetGyn(config)} at {dt}.").GetAwaiter();
            loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, "SnakeGame", $"Scheduled snake in {mainClient.GetGyn(config)} at {dt}.")).GetAwaiter();

            // For debuggin
            //dt = DateTime.Now;//.AddDays(dt.Day - DateTime.Now.Day);
            //dt = dt.AddMinutes(1);

            return dt;
        }
    }
}
