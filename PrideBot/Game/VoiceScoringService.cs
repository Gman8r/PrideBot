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
        Dictionary<ulong, TimeSpan> UserVoiceTimes;

        public VoiceScoringService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService, UserRegisteredCache userReg)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;
            this.loggingService = loggingService;
            this.userReg = userReg;

            client.Ready += ClientReady;
        }

        private Task ClientReady()
        {
            DoCheckLoop().GetAwaiter();
            return Task.CompletedTask;
        }

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
                UserVoiceTimes ??= new Dictionary<ulong, TimeSpan>();

                var guild = client.GetGyn(config);
                var minMInutes = int.Parse(config["voiechatminutes"]);
                var lastLoopTime = DateTime.Now;
                while (true)
                {
                    if (!GameHelper.IsEventOccuring(config))
                        break;

                    //Console.WriteLine();
                    //foreach (var uvt in UserVoiceTimes)
                    //{
                    //    Console.WriteLine($"{uvt.Key} {uvt.Value.TotalSeconds}");
                    //}

                    if (DateTime.Now.Day != currentDay)
                    {
                        foreach (var key in UserVoiceTimes.Keys)
                        {
                            UserVoiceTimes[key] = TimeSpan.FromMinutes(0);
                        }
                        currentDay = DateTime.Now.Day;
                    }

                    var currentLoopTime = DateTime.Now;
                    foreach (var voiceChannel in guild.VoiceChannels)
                    {
                        var users = voiceChannel.Users.Where(a => !a.IsBot);
                        if (users.Count() > 1)
                        {
                            foreach (var user in users)
                            {
                                // User is not registered, just move on
                                if (!(await userReg.GetOrDownloadAsync(user.Id.ToString())))
                                    continue;
                                // FIrst time we've seen the user in a voice chat this runtime
                                else if (!UserVoiceTimes.ContainsKey(user.Id))
                                {
                                    // Check if they've already gotten the achievement today
                                    using var connection = repo.GetDatabaseConnection();
                                    await connection.OpenAsync();
                                    var lastScore = await repo.GetLastScoreFromUserAndAchievementAsync(connection, user.Id.ToString(), "VOICE");

                                    // If so, just give em the required minutes x2 so they def dont get the achievement
                                    if (lastScore != null && lastScore.TimeStamp.Day == currentDay)
                                        UserVoiceTimes[user.Id] = TimeSpan.FromMinutes(minMInutes * 2);
                                    else
                                        UserVoiceTimes[user.Id] = TimeSpan.FromMinutes(0);
                                }
                                // If they have more than the required amount of minutes we've already given the achievement to them today
                                if (UserVoiceTimes[user.Id].TotalMinutes >= minMInutes)
                                    continue;

                                // Now add however much time has passed and see if it's time to give the achievmeent
                                UserVoiceTimes[user.Id] += currentLoopTime - lastLoopTime;
                                if (UserVoiceTimes[user.Id].TotalMinutes >= minMInutes)
                                {
                                    using var connection = repo.GetDatabaseConnection();
                                    await connection.OpenAsync();
                                    await scoringService.AddAndDisplayAchievementAsync(connection, user, "VOICE", client.CurrentUser);
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
