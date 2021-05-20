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
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Quizzes;
using PrideBot.Game;


namespace PrideBot.Quizzes
{
    class QuizService
    {

        const int QuizCancelBufferMinutes = 15;

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;

        GuildSettings guildSettings;
        SocketTextChannel quizChannel;

        public QuizService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService)
        {
            this.repo = repo;
            this.config = config;
            this.client = client;
            this.scoringService = scoringService;

            client.Ready += ClientReady;
            this.loggingService = loggingService;
        }

        private Task ClientReady()
        {
            DoQuizLoop().GetAwaiter();
            return Task.CompletedTask;
        }

        async Task DoQuizLoop()
        {
            try
            {
                using var connection = DatabaseHelper.GetDatabaseConnection();
                await connection.OpenAsync();
                guildSettings = await GetGuildSettingsAsync(connection);
                await connection.CloseAsync();
                quizChannel = client.GetGyn(config).GetChannelfromConfig(config, "quizchannel") as SocketTextChannel;

                while (true)
                {
                    await Task.Delay(500);
                    if (!GameHelper.EventStarted(config))
                        continue;
                    if (guildSettings.QuizOpen && GameHelper.GetQuizDay(DateTime.Now.AddMinutes(QuizCancelBufferMinutes)) != guildSettings.QuizDay)
                    {
                        await connection.OpenAsync();
                        await CloseQuizAsync(connection, guildSettings.QuizDay);
                        await connection.CloseAsync();
                    }
                    else if (!guildSettings.QuizOpen && GameHelper.GetQuizDay() != guildSettings.QuizDay)
                    {
                        await connection.OpenAsync();
                        await OpenQuizAsync(connection, GameHelper.GetQuizDay());
                        await connection.CloseAsync();
                    }
                }
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false);
                await quizChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
        }

        public bool IsQuizOpen => guildSettings.QuizOpen;

        public int ActiveQuiz => guildSettings.QuizDay;

        async Task CloseQuizAsync(SqlConnection connection, int day)
        {
            guildSettings.QuizOpen = false;
            await repo.UpdateGuildSettingsAsync(connection, guildSettings);
            await quizChannel.SendMessageAsync("closed quiz " + day);
        }


        async Task OpenQuizAsync(SqlConnection connection, int day)
        {
            guildSettings.QuizDay = day;
            guildSettings.QuizOpen = true;
            await repo.UpdateGuildSettingsAsync(connection, guildSettings);
            await quizChannel.SendMessageAsync("opened quiz " + day);
        }

        async Task<GuildSettings> GetGuildSettingsAsync(SqlConnection connection)
        {
            return await repo.GetOrCreateGuildSettingsAsync(connection, client.GetGyn(config).Id.ToString());
        }
    }
}
