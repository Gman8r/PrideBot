﻿using Discord;
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
        readonly DiscordSocketClient mainClient;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly UserRegisteredCache userReg;
        readonly TokenConfig tokenConfig;
        readonly IServiceProvider provider;

        DiscordSocketClient tsuchiClient;
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

            client.Ready += MainClientReady;
            this.provider = provider;
        }

        private Task MainClientReady()
        {
            Task.Run(async () =>
            {
                tsuchiClient = new DiscordSocketClient();
                await tsuchiClient.LoginAsync(TokenType.Bot, tokenConfig["tsuchitoken"]);
                await tsuchiClient.StartAsync();

                tsuchiClient.Ready += DoGameLoop;
                mainClient.UserVoiceStateUpdated += UserVoiceStateUpdated;
            }).GetAwaiter();
            return Task.CompletedTask;
        }

        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if (tsuchiConnectedChannel == null || user.IsBot || after.VoiceChannel.Id != tsuchiConnectedChannel.Id)
                return Task.CompletedTask;
            var channel = tsuchiConnectedChannel;
            tsuchiConnectedChannel = null;
            Task.Run(async () =>
            {
                await channel.DisconnectAsync();
                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                await scoringService.AddAndDisplayAchievementAsync(connection, user, "SNAKE", tsuchiClient.CurrentUser);
            }).GetAwaiter();
            return Task.CompletedTask;
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

                        var tsuchiChannels = tsuchiClient.GetGyn(config).VoiceChannels
                            .Where(a => !a.Users.Any())
                            .ToArray();
                        if (tsuchiChannels.Any())
                        {
                            tsuchiConnectedChannel = tsuchiChannels[rand.Next() % tsuchiChannels.Length];
                            int tries = 0;
                            while (tries < 100)
                            {
                                tries++;
                                try
                                {
                                    await tsuchiConnectedChannel.ConnectAsync();
                                    break;
                                }
                                catch
                                {
                                    Console.WriteLine($"Failed try #{tries} to connect Tsuchi to voice.");
                                }
                            }
                            var endTime = nextSnakeTime.AddMinutes(await GetVoiceMinutesAsync());
                            while (DateTime.Now < endTime && tsuchiConnectedChannel != null)
                            {
                                await Task.Delay(1000);
                            }
                            if (tsuchiConnectedChannel != null)
                            {
                                var channel = tsuchiConnectedChannel;
                                tsuchiConnectedChannel = null;
                                await channel.DisconnectAsync();
                            }

                        }

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
            //dt = dt.AddSeconds(30);

            return dt;
        }
    }
}
