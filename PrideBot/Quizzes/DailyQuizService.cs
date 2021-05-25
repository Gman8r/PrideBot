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

        GuildSettings guildSettings;
        SocketTextChannel quizChannel;
        SocketTextChannel quizDiscussionChannel;
        SocketRole quizTakenRole;

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

        async Task DoQuizLoop()
        {
            try
            {
                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                guildSettings = await GetGuildSettingsAsync(connection);
                await connection.CloseAsync();
                var gyn = await client.AwaitGyn(config);
                quizChannel = gyn.GetChannelFromConfig(config, "quizchannel") as SocketTextChannel;
                quizDiscussionChannel = gyn.GetChannelFromConfig(config, "quizdiscussionchannel") as SocketTextChannel;
                quizTakenRole = gyn.GetRoleFromConfig(config, "quiztakenrole");

                while (true)
                {
                    await Task.Delay(500);
                    if (!GameHelper.IsEventOccuring(config))
                        continue;

                    // last 15 minutes of day, quiz should be closed
                    var isEndOfDay = GameHelper.GetQuizDay(DateTime.Now.AddMinutes(QuizCancelBufferMinutes)) != GameHelper.GetQuizDay();

                    if (guildSettings.QuizOpen && (isEndOfDay || guildSettings.QuizDay != GameHelper.GetQuizDay()))
                    {
                        if (guildSettings.QuizOpen)
                        {
                            await connection.OpenAsync();
                            await CloseQuizAsync(connection, guildSettings.QuizDay);
                            await connection.CloseAsync();
                        }
                    }
                    if (!guildSettings.QuizOpen && !isEndOfDay)
                    {
                        if (!guildSettings.QuizOpen)
                        {
                            await connection.OpenAsync();
                            await OpenQuizAsync(connection, GameHelper.GetQuizDay());
                            await connection.CloseAsync();
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

        public bool IsQuizOpen => guildSettings.QuizOpen;

        public int ActiveQuiz => guildSettings.QuizDay;

        async Task CloseQuizAsync(SqlConnection connection, int day)
        {
            var quizzes = await repo.GetQuizzesForDayAsync(connection, day.ToString());
            var embed = GetQuizReviewEmbed(quizzes.ToList())
                .WithTitle("Daily Quiz Closed")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_CLOSED", day));

            guildSettings.QuizOpen = false;
            await repo.UpdateGuildSettingsAsync(connection, guildSettings);

            await quizChannel.SendMessageAsync(embed: embed.Build());

            //foreach (var member in quizTakenRole.Members)
            //{
            //    await member.RemoveRoleAsync(quizTakenRole);
            //}
        }


        async Task OpenQuizAsync(SqlConnection connection, int day)
        {
            foreach (var member in quizTakenRole.Members)
            {
                await member.RemoveRoleAsync(quizTakenRole);
            }

            var oldPins = (await quizDiscussionChannel.GetPinnedMessagesAsync()).Select(msg => (msg.Channel, msg))
                .Concat((await quizChannel.GetPinnedMessagesAsync()).Select(msg => (msg.Channel, msg)));
            foreach (var pin in oldPins)
            {
                var userMsg = await pin.Channel.GetMessageAsync(pin.msg.Id) as IUserMessage;
                await userMsg.UnpinAsync();
            }

            var quizzes = (await repo.GetQuizzesForDayAsync(connection, day.ToString())).ToList();
            var embed = EmbedHelper.GetEventEmbed(null, config, showUser: false)
                .WithTitle("Daily Quiz OPEN")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_OPEN", day, config.GetDefaultPrefix()));

            var categoryField = new EmbedFieldBuilder()
                .WithName("Hold")
                .WithValue("Hold");
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

            guildSettings.QuizDay = day;
            guildSettings.QuizOpen = true;
            await repo.UpdateGuildSettingsAsync(connection, guildSettings);

            var quizMsg = await quizChannel.SendMessageAsync(embed: embed.Build());
            await quizMsg.PinAsync();


            embed = GetQuizReviewEmbed(quizzes)
                .WithTitle("Daily Quiz Review")
                .WithDescription(DialogueDict.Get("DAILY_QUIZ_REVIEW", day));
            var reviewMsg = await quizDiscussionChannel.SendMessageAsync(embed: embed.Build());
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
