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
    class VoiceScoringService
    {

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;
        readonly UserRegisteredCache userReg;

        int currentDay;
        Dictionary<ulong, UserVoiceData> voiceData;
        public class UserVoiceData
        {
            public ulong Id;
            public Optional<IVoiceState> voiceState;
            public TimeSpan voiceTime;
            public TimeSpan streamTime;

            public UserVoiceData(ulong id, Optional<IVoiceState> voiceState)
            {
                Id = id;
                this.voiceState = voiceState;
            }
        }

        public VoiceScoringService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, UserRegisteredCache userReg)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.userReg = userReg;

            voiceData = new Dictionary<ulong, UserVoiceData>();
            client.Ready += ClientReady;
            client.UserVoiceStateUpdated += VoiceStateUpdated;
        }

        private async Task VoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if ((after.VoiceChannel?.Guild?.Id ?? (ulong)0) != ulong.Parse(config["ids:gyn"]))
                return;

            if (!voiceData.ContainsKey(user.Id))
                await AddUserDataAsync(user, new Optional<IVoiceState>(after as IVoiceState));
            else
                voiceData[user.Id].voiceState = after;
        }

        private async Task AddUserDataAsync(SocketUser user, Optional<IVoiceState> state)
        {
            voiceData[user.Id] = new UserVoiceData(user.Id, state);

            // Check if they've already gotten either achievement today
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var lastVoiceScore = await repo.GetLastScoreFromUserAndAchievementAsync(connection, user.Id.ToString(), "VOICE");
            var lastStreamScore = await repo.GetLastScoreFromUserAndAchievementAsync(connection, user.Id.ToString(), "STREAM");
            var minMInutes = int.Parse(config["voiechatminutes"]);

            // If so, just give em the required minutes x2 so they defo dont get the achievement
            if (lastVoiceScore != null && lastVoiceScore.Timestamp.Day == currentDay)
                voiceData[user.Id].voiceTime = TimeSpan.FromMinutes(minMInutes * 2);
            if (lastStreamScore != null && lastStreamScore.Timestamp.Day == currentDay)
                voiceData[user.Id].streamTime = TimeSpan.FromMinutes(minMInutes * 2);
        }

        private Task ClientReady()
        {
            DoCheckLoop().GetAwaiter();
            return Task.CompletedTask;
        }

        DateTime GetNow() => DateTime.Now;  // For debugging

        async Task DoCheckLoop()
        {
            try
            {
                while(!GameHelper.IsEventOccuring(config))
                {
                    await Task.Delay(10 * 60000);
                }

                if (currentDay == 0)
                    currentDay = DateTime.Now.Day;

                var guild = client.GetGyn(config);
                var minMinutes = int.Parse(config["voiechatminutes"]);
                var lastLoopTime = DateTime.Now;
                while (true)
                {
                    if (!GameHelper.IsEventOccuring(config))
                        break;

                    if (GetNow().Day != currentDay)
                    {
                        var keys = voiceData.Keys.ToArray();    // Why do I have to do this to say it's not modifying the collection?
                        foreach (var key in keys)
                        {
                            voiceData[key].voiceTime = TimeSpan.FromMinutes(0);
                            voiceData[key].streamTime = TimeSpan.FromMinutes(0);
                        }
                        currentDay = GetNow().Day;
                    }

                    var currentLoopTime = DateTime.Now;
                    foreach (var voiceChannel in guild.VoiceChannels)
                    {
                        var users = voiceChannel.Users.Where(a => !a.IsBot);
                        if (users.Count() > 1)
                        {
                            foreach (var user in users)
                            {
                                // If user is not registered, just move on
                                if (!(await userReg.GetOrDownloadAsync(user.Id.ToString())))
                                    continue;

                                // Create user if necessary, maybe the bot started up mid-call
                                if (!voiceData.ContainsKey(user.Id))
                                    await AddUserDataAsync(user, Optional<IVoiceState>.Unspecified);

                                // If they have more than the required amount of minutes we've already given the achievement to them today
                                if (voiceData[user.Id].voiceTime.TotalMinutes < minMinutes)
                                {
                                    // Now add however much time has passed and see if it's time to give the achievmeent
                                    voiceData[user.Id].voiceTime += currentLoopTime - lastLoopTime;
                                    if (voiceData[user.Id].voiceTime.TotalMinutes >= minMinutes)
                                    {
                                        using var connection = repo.GetDatabaseConnection();
                                        await connection.OpenAsync();
                                        await scoringService.AddAndDisplayAchievementAsync(connection, user, "VOICE", client.CurrentUser, DateTime.Now);
                                    }
                                }

                                // Repeat with stream if they're streaming
                                if (voiceData[user.Id].voiceState.IsSpecified
                                    && voiceData[user.Id].voiceState.Value.IsStreaming
                                    && voiceData[user.Id].streamTime.TotalMinutes < minMinutes)
                                {
                                    // Now add however much time has passed and see if it's time to give the achievmeent
                                    voiceData[user.Id].streamTime += currentLoopTime - lastLoopTime;
                                    if (voiceData[user.Id].streamTime.TotalMinutes >= minMinutes)
                                    {
                                        using var connection = repo.GetDatabaseConnection();
                                        await connection.OpenAsync();
                                        await scoringService.AddAndDisplayAchievementAsync(connection, user, "STREAM", client.CurrentUser, DateTime.Now);
                                    }
                                }

                            }
                        }
                    }
                    lastLoopTime = DateTime.Now;
                    await Task.Delay(10000);
                }
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false)
                    .WithTitle($"Exception in {this.GetType().Name} Module");
                var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                await modChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
            finally
            {
                if (GameHelper.IsEventOccuring(config))
                {
                    var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                    await modChannel.SendMessageAsync("The module will be attempt to re-enable in 30 minutes.");
                    await Task.Delay(30 * 60000);
                    DoCheckLoop().GetAwaiter();
                }
            }
        }
    }
}
