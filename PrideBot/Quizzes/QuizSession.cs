﻿using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Game;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Quizzes
{
    public class QuizSession : DMSession
    {
        const int TimerSeconds = 60;

        readonly ModelRepository repo;
        readonly ScoringService scoringService;

        bool sessionComplete = false;
        string correctChoice;
        QuizLog quizLog;

        public QuizSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, ScoringService scoringService) : base(channel, user, config, client, timeout, originmessage)
        {
            this.repo = repo;
            this.scoringService = scoringService;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {
            var day = GameHelper.GetQuizDay();
            using var connection = DatabaseHelper.GetDatabaseConnection();
            await connection.OpenAsync();

            quizLog = await repo.GetOrCreateQuizLogAsync(connection, user.Id.ToString(), day.ToString());
            if (quizLog.Attempted)
            {
                await channel.SendMessageAsync(embed: EmbedHelper.GetEventErrorEmbed(user, DialogueDict.Get("QUIZ_ATTEMPTED"), client, showUser: false).Build());
                await channel.SendMessageAsync(embed: EmbedHelper.GetEventErrorEmbed(user, "(but that's ok we're debugging so just continue)", client, showUser: false).Build());
                //return;
            }

            var availableQuizzes = (await repo.GetQuizzesForDayAsync(connection, day.ToString())).ToList();

            var embed = GetEmbed()
                .WithTitle("Daily Quiz")
                .WithDescription(DialogueDict.Get("QUIZ_BEGIN"));
            if (availableQuizzes.Count() > 1)
                embed.Description += "\n\n" + DialogueDict.Get("QUIZ_MULTIPLE", availableQuizzes.Count());
            embed.Description += "\n\n" + DialogueDict.Get("QUIZ_INSTRUCTIONS", TimerSeconds);
            embed.Description += "\n\n" + DialogueDict.Get(availableQuizzes.Count() > 1 ? "QUIZ_CONFIRM_MULTIPLE" : "QUIZ_CONFIRM");

            var categoryField = new EmbedFieldBuilder()
                .WithName("Hold")
                .WithValue("Hold");
            if (availableQuizzes.Count() > 1)
            {
                var fieldValue = "";
                for (int i = 0; i < availableQuizzes.Count; i++)
                {
                    var quizOption = availableQuizzes[i];
                    if (!string.IsNullOrEmpty(categoryField.Value.ToString()))
                        fieldValue += "\n";
                    fieldValue += $"{EmoteHelper.NumberEmotes[i+1]} - {quizOption.Category}";
                    categoryField.Name = "Today's Category Choices:";
                }
                categoryField.Value = fieldValue;
            }
            else
            {
                categoryField.Name = "Today's Quiz Category:";
                categoryField.Value = $"❓ {availableQuizzes.FirstOrDefault().Category}";
            }
            embed.AddField(categoryField);

            Prompt response;
            if (availableQuizzes.Count == 1)
                response = await SendAndAwaitYesNoResponseAsync(embed: embed);
            else
            {
                var numberEmoteChoices = Enumerable.Range(1, availableQuizzes.Count)
                    .Select(a => EmoteHelper.GetNumberEmote(a))
                    .ToList();
                numberEmoteChoices.Add(NoEmote);
                response = await SendAndAwaitEmoteResponseAsync(embed: embed, emoteChoices: numberEmoteChoices);
            }
            var quizChoice = 0;
            if (response.IsNo)
            {
                await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                return;
            }
            else if (!response.IsYes)
            {
                var selection = EmoteHelper.NumberEmotes.ToList()
                    .FindIndex(a => a.Equals(response.EmoteResponse.ToString()));
                quizChoice = selection - 1;
            }

            quizLog.Attempted = true;
            quizLog.Correct = false;
            var b = await repo.UpdateQuizLogAsync(connection, quizLog);

            var quiz = availableQuizzes[quizChoice];
            var rand = new Random();
            correctChoice = quiz.Correct.Split('\n')[rand.Next() % quiz.Correct.Split('\n').Length];
            var incorrectChoicePool = quiz.Incorrect.Split('\n').ToList();
            var choices = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var answer = incorrectChoicePool[rand.Next() % incorrectChoicePool.Count];
                choices.Add(answer);
                incorrectChoicePool.Remove(answer);
            }
            var correctIndex = rand.Next() % 4;
            choices.Insert(correctIndex, correctChoice);

            var textChoices = new List<char>() { 'A', 'B', 'C', 'D' };
            var emoteChoices = textChoices
            .Select(a => EmoteHelper.GetLetterEmote(a))
            .ToList();
            embed = GetEmbed()
                .WithTitle("Quiz Question")
                .WithDescription($"{quiz.Question}");
            embed.AddField("Choices:", string.Join("\n", choices.Select(a => $"{emoteChoices[choices.IndexOf(a)]} {a}")));

            response = await SendResponseAsync(embed: embed, emoteChoices: emoteChoices, alwaysPopulateEmotes: true);
            RunTimer().GetAwaiter();
            await AwaitCurrentResponseAsync();
            var attemptsLeft = 3;
            while (attemptsLeft > 0)
            {
                int chosenIndex;
                if (response.EmoteResponse != null)
                {
                    chosenIndex = emoteChoices.FindIndex(a => a.ToString().Equals(response.EmoteResponse.ToString()));
                }
                else
                {
                    if (response.MessageResponse.Content.Length != 1 || !textChoices.Contains(response.MessageResponse.Content.ToUpper()[0]))
                    {
                        await channel.SendMessageAsync(DialogueDict.Get("QUIZ_BAD_PARSE"));
                        await AwaitCurrentResponseAsync();
                        continue;
                    }
                    chosenIndex = response.MessageResponse.Content.ToUpper()[0] - 'A';
                }
                var chosenLetter = ((char)('A' + chosenIndex)).ToString();
                if (attemptsLeft == 3)
                    quizLog.Guess1 = choices[chosenIndex];
                else if (attemptsLeft == 2)
                    quizLog.Guess2 = choices[chosenIndex];
                else if (attemptsLeft == 1)
                    quizLog.Guess3 = choices[chosenIndex];
                if (chosenIndex != correctIndex)
                {
                    if (attemptsLeft > 1)
                    {
                        await channel.SendMessageAsync(DialogueDict.Get($"QUIZ_WRONG_{4 - attemptsLeft}", chosenLetter));
                        await AwaitCurrentResponseAsync();
                    }
                }
                else
                {
                    quizLog.Correct = true;
                    break;
                }
                attemptsLeft--;
            }

            sessionComplete = true;
            if (IsCancelled)
                return;

            var correct = attemptsLeft > 0;
            var guesses = 4 - attemptsLeft;
            quizLog.Guesses = guesses;
            await repo.UpdateQuizLogAsync(connection, quizLog);

            embed = GetEmbed();
            embed.Title = correct ? $"✅ Kuh-rect!! ({new List<string>() { "Zeroth", "First", "Second", "Third" }[guesses]} Attempt)" : "❌ Incorrect";
            if (correct)
                embed.Description = DialogueDict.Get($"QUIZ_CORRECT_{guesses}");
            else
                embed.Description = DialogueDict.Get($"QUIZ_INCORRECT", correctChoice, user.Queen(client));
            embed.Description += "\n\n" + DialogueDict.Get("QUIZ_CLOSING");
            await channel.SendMessageAsync(embed: embed.Build());
            await AddScore(connection);
        }

        async Task AddScore(SqlConnection connection)
        {
            var achievementId = quizLog.Correct ? $"QUIZ_CORRECT_{quizLog.Guesses}" : "QUIZ_PARTICIPATE";
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, client.CurrentUser);
        }

        async Task RunTimer()
        {
            var endTime = DateTime.Now.AddSeconds(TimerSeconds);
            while (DateTime.Now < endTime.AddSeconds(-30))
                await Task.Delay(50);
            if (!sessionComplete)
                await channel.SendMessageAsync(DialogueDict.Get("QUIZ_30_SECONDS"));
            while (DateTime.Now < endTime.AddSeconds(-15))
                await Task.Delay(50);
            if (!sessionComplete)
                await channel.SendMessageAsync(DialogueDict.Get("QUIZ_15_SECONDS"));
            while (DateTime.Now < endTime.AddSeconds(-5))
                await Task.Delay(50);
            if (!sessionComplete)
                await channel.SendMessageAsync(DialogueDict.Get("QUIZ_5_SECONDS"));
            while (DateTime.Now < endTime)
                await Task.Delay(50);
            if (!sessionComplete)
            {
                Cancel(DialogueDict.Get("QUIZ_TIMEOUT", correctChoice, user.Queen(client))
                    + "\n\n" + DialogueDict.Get("QUIZ_CLOSING"));

                quizLog.Guesses = 3;
                using var connection = DatabaseHelper.GetDatabaseConnection();
                await connection.OpenAsync();
                await repo.UpdateQuizLogAsync(connection, quizLog);

                await Task.Delay(500);
                await AddScore(connection);
            }
        }
    }
}
