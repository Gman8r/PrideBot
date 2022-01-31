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
    class DailyQuizService
    {

        const int QuizCancelBufferMinutes = 15;

        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;
        readonly LoggingService loggingService;

        SocketTextChannel quizChannel;
        SocketRole quizTakenRole;
        QuizSettings quizSettings;

        public DailyQuizService(ModelRepository repo, IConfigurationRoot config, DiscordSocketClient client, ScoringService scoringService, LoggingService loggingService)
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

        private class QuizSettings
        {
            public int day;
            public bool open;
        }

        async Task DoQuizLoop()
        {
            try
            {
                quizSettings = await GetQuizSettingsAsync();
                var gyn = await client.AwaitGyn(config);
                quizChannel = gyn.GetChannelFromConfig(config, "quizchannel") as SocketTextChannel;
                quizTakenRole = gyn.GetRoleFromConfig(config, "quiztakenrole");

                while (true)
                {
                    await Task.Delay(500);
                    if (!GameHelper.IsEventOccuring(config))
                        continue;

                    // last 15 minutes of day, quiz should be closed
                    var isEndOfDay = GameHelper.GetEventDay(config, DateTime.Now.AddMinutes(QuizCancelBufferMinutes)) != GameHelper.GetEventDay(config);

                    if (quizSettings.open && (isEndOfDay || quizSettings.day != GameHelper.GetEventDay(config)))
                    {
                        if (quizSettings.open)
                        {
                            await CloseQuizAsync(quizSettings.day);
                        }
                    }
                    if (!quizSettings.open && !isEndOfDay)
                    {
                        if (!quizSettings.open)
                        {
                            await OpenQuizAsync(GameHelper.GetEventDay(config));
                        }
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

        async Task<QuizSettings> GetQuizSettingsAsync()
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var guildSettings = await GetGuildSettingsAsync(connection);
            return new QuizSettings()
            {
                day = guildSettings.QuizDay,
                open = guildSettings.QuizOpen
            };
        }

        async Task UpdateQuizSettingsAsync(QuizSettings value)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var guildSettings = await GetGuildSettingsAsync(connection);
            guildSettings.QuizDay = value.day;
            guildSettings.QuizOpen = value.open;
            await repo.UpdateGuildSettingsAsync(connection, guildSettings);
        }

        public bool IsQuizOpen => quizSettings.open;

        public int ActiveQuiz => quizSettings.day;

        async Task CloseQuizAsync(int day)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var quizzes = await repo.GetQuizzesForDayAsync(connection, day.ToString());
            var embed = GetQuizReviewEmbed(quizzes.ToList())
                .WithTitle("Daily Quiz Closed")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_CLOSED", day));

            var components = new ComponentBuilder()
                .WithButton("💬 Discuss It!", $"QUIZ.D:{day},{-1}");

            quizSettings.open = false;
            await UpdateQuizSettingsAsync(quizSettings);

            await quizChannel.SendMessageAsync(embed: embed.Build(), components: components.Build());
        }


        async Task OpenQuizAsync(int day)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            foreach (var member in quizTakenRole.Members)
            {
                await member.RemoveRoleAsync(quizTakenRole);
            }

            var oldPins = (await quizChannel.GetPinnedMessagesAsync()).Select(msg => (msg.Channel, msg));
            foreach (var pin in oldPins)
            {
                var userMsg = await pin.Channel.GetMessageAsync(pin.msg.Id) as IUserMessage;
                await userMsg.UnpinAsync();
            }

            // TODO switch over

            var discussionThread = !Startup.DebugMode
                ? await quizChannel.CreateThreadAsync($"Quiz Discussion: Day {day}", ThreadType.PrivateThread, invitable: false)
                : await quizChannel.CreateThreadAsync($"Quiz Discussion Day {day}", ThreadType.PublicThread);

            var quizzes = (await repo.GetQuizzesForDayAsync(connection, day.ToString())).ToList();
            var embed = EmbedHelper.GetEventEmbed(null, config, showUser: false)
                .WithTitle("Daily Quiz OPEN")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_OPEN", day, config.GetDefaultPrefix()));

            var categoryField = new EmbedFieldBuilder()
                .WithName("PLACEHOLDER")
                .WithValue("PLACEHOLDER");
            if (quizzes.Count() > 1)
            {
                var fieldValue = "";
                for (int i = 0; i < quizzes.Count; i++)
                {
                    var quizOption = quizzes[i];
                    if (!string.IsNullOrEmpty(categoryField.Value.ToString()))
                        fieldValue += "\n";
                    fieldValue += $"{EmoteHelper.NumberEmotes[i + 1]} - {quizOption.Category}";
                    categoryField.Name = "Today's Category Choices:";
                }
                categoryField.Value = fieldValue;
            }
            else
            {
                categoryField.Name = "Today's Quiz Category:";
                
                categoryField.Value = $"❓ {quizzes.FirstOrDefault().Category}";
            }
            embed.AddField(categoryField);

            quizSettings.day = day;
            quizSettings.open = true;
            await UpdateQuizSettingsAsync(quizSettings);

            var quizMsg = await quizChannel.SendMessageAsync(embed: embed.Build());
            await quizMsg.PinAsync();


            embed = GetQuizReviewEmbed(quizzes)
                .WithTitle("Daily Quiz Review")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_REVIEW", day));
            var reviewMsg = await discussionThread.SendMessageAsync(embed: embed.Build());
            await reviewMsg.PinAsync();


        }

        async Task<GuildSettings> GetGuildSettingsAsync(SqlConnection connection)
        {
            
            return await repo.GetOrCreateGuildSettingsAsync(connection, config["ids:gyn"]);
        }

        EmbedBuilder GetQuizReviewEmbed(List<Quiz> quizzes)
        {
            var embed = EmbedHelper.GetEventEmbed(null, config, showUser: false);
            for (int i = 0; i < quizzes.Count; i++)
            {
                var quiz = quizzes[i];
                embed.AddField("\u200B", "\u200B");
                var answers = quiz.Correct.Split("\n").ToList();
                embed.AddField(quizzes.Count > 1 ? $"Quiz Option {i + 1} ({quiz.Category}):" : $"Quiz Question ({quiz.Category}):", quiz.Question);
                embed.AddField(answers.Count > 1 ? "Possible Answers:" : "Answer:", 
                    string.Join("\n", answers));
            }
            return embed;
        }
    }
}
