using Discord;
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
    public class QuizSession : Session
    {
        const int TimerSeconds = 60;

        readonly ModelRepository repo;
        readonly GuildSettings guildSettings;
        readonly ScoringService scoringService;

        bool sessionComplete = false;
        string correctChoice;
        QuizLog quizLog;
        List<Quiz> availableQuizzes;
        int chosenQuizIndex;

        public QuizSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, ScoringService scoringService, QuizLog quizLog, GuildSettings guildSettings) : base(channel, user, config, client, timeout, originmessage)
        {
            this.repo = repo;
            this.scoringService = scoringService;
            this.quizLog = quizLog;
            this.guildSettings = guildSettings;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {
            var day = quizLog.Day;
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            //quizLog = await repo.GetOrCreateQuizLogAsync(connection, user.Id.ToString(), day.ToString());
            //if (quizLog.Attempted)
            //{
            //    await channel.SendMessageAsync(embed: EmbedHelper.GetEventErrorEmbed(user, DialogueDict.Get("QUIZ_ATTEMPTED"), client, showUser: false).Build());
            //    await channel.SendMessageAsync(embed: EmbedHelper.GetEventErrorEmbed(user, "(but that's ok we're debugging so just continue)", client, showUser: false).Build());
            //    //return;
            //}

            availableQuizzes = (await repo.GetQuizzesForDayAsync(connection, day.ToString())).ToList();

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
                //var fieldValue = "";
                //for (int i = 0; i < availableQuizzes.Count; i++)
                //{
                //    var quizOption = availableQuizzes[i];
                //    if (!string.IsNullOrEmpty(categoryField.Value.ToString()))
                //        fieldValue += "\n";
                //    fieldValue += $"{EmoteHelper.NumberEmotes[i+1]} - {quizOption.Category}";
                //    categoryField.Name = "Today's Category Choices:";
                //}
                //categoryField.Value = fieldValue;
            }
            else
            {
                categoryField.Name = "Today's Quiz Category:";
                categoryField.Value = $"❓ {availableQuizzes.FirstOrDefault().Category}";
                embed.AddField(categoryField);
            }

            var components = new ComponentBuilder();
            if (availableQuizzes.Count == 1)
            {
                components.WithButton("Ready!", "YES", ButtonStyle.Success, ThumbsUpEmote);
                components.WithButton("Nevermind, Not Yet", "NO", ButtonStyle.Secondary, NoEmote);
            }
            else
            {

                for (int i = 0; i < availableQuizzes.Count; i++)
                {
                    var quizOption = availableQuizzes[i];
                    components.WithButton(quizOption.Category, i.ToString(), ButtonStyle.Secondary, EmoteHelper.GetNumberEmote(i + 1));
                }
                components.WithButton("Nevermind, Not Yet", "NO", ButtonStyle.Secondary, NoEmote);
            }
            var response = await SendAndAwaitNonTextResponseAsync(embed: embed, components: components);
            chosenQuizIndex = 0;
            if (response.IsNo)
            {
                await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                return;
            }
            else if (!response.IsYes)
            {
                var selection = int.Parse(response.InteractionResponse.Data.CustomId);
                chosenQuizIndex = selection;
            }

            var quiz = availableQuizzes[chosenQuizIndex];
            quizLog.Attempted = true;
            quizLog.Correct = false;
            quizLog.QuizId = quiz.QuizId;
            await repo.UpdateQuizLogAsync(connection, quizLog);

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

            embed = GetEmbed()
                .WithTitle("Quiz Question")
                .WithDescription($"{quiz.Question}");

            var choiceStr = "";
            for (int i = 0; i < choices.Count; i++)
            {
                choiceStr += $"\n{EmoteHelper.GetLetterEmote((char)('A' + i))} {choices[i]}";
            }
            embed.AddField("Choices:",
                choiceStr.Trim());

            var choicesComponents = new ComponentBuilder();
            for (int i = 0; i < choices.Count; i++)
            {
                // Restrict choice to 80 chars just in case
                choices[i] = choices[i].Substring(0, Math.Min(choices[i].Length, 80));
                var choice = choices[i];
                choicesComponents.WithButton(" ", i.ToString(), ButtonStyle.Secondary, EmoteHelper.GetLetterEmote((char)('A' + i)));
            }

            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, user.Id.ToString());
            var halfPlushie = userPlushies.FirstOrDefault(a => a.PlushieId.Equals("QUIZ_HALF"));
            if (halfPlushie != null)
                choicesComponents.WithButton("Remove 2 Options (Use Plushie)", "QUIZ_HALF", ButtonStyle.Secondary, new Emoji("2️⃣"));

            response = await SendResponseAsync(embed: embed, components: choicesComponents, acceptsText: false);
            var quizMessage = response.BotMessage;
            RunTimer().GetAwaiter();
            var attemptsLeft = 3;
            quizLog.Guess1 = null;
            quizLog.Guess2 = null;
            quizLog.Guess3 = null;
            while (attemptsLeft > 0)
            {
                await AwaitCurrentResponseAsync();
                if (response.InteractionResponse.Data.CustomId.Equals("QUIZ_HALF"))  // plushie
                {
                    if (quizLog.Guess1 == null)
                    {
                        var wrongIndexPool = choices
                            .Select(a => choices.IndexOf(a))
                            .Where(a => a != correctIndex)
                            .ToList();
                        for (int i = 0; i < 2; i++)
                        {
                            var strikeout = wrongIndexPool[rand.Next() % wrongIndexPool.Count];
                            wrongIndexPool.Remove(strikeout);

                            // Disable button with matching index
                            var newButton = (choicesComponents.ActionRows.FirstOrDefault().Components[strikeout] as ButtonComponent)
                                .ToBuilder().WithDisabled(true).Build();
                            choicesComponents.ActionRows.FirstOrDefault().Components[strikeout] = newButton;
                            // and strikeout choice in embed
                            var strikeoutText = embed.Fields.Last().Value.ToString().Trim().Split('\n')[strikeout].Trim();
                            embed.Fields.Last().Value = embed.Fields.Last().Value.ToString().Replace(strikeoutText, $"~~{strikeoutText}~~");
                        }
                        await response.BotMessage.ModifyAsync(a =>
                        {
                            a.Embed = embed.Build();
                            a.Components = choicesComponents.Build();
                        });
                        await repo.DepleteUserPlushieAsync(connection, halfPlushie.UserPlushieId, DateTime.Now, false, PlushieUseContext.Quiz, quiz.QuizId.ToString());
                        await channel.SendMessageAsync(DialogueDict.Get($"QUIZ_PLUSHIE_HALF"));
                    }
                    else
                    {
                        await channel.SendMessageAsync(DialogueDict.Get($"QUIZ_PLUSHIE_HALF_FAIL"));
                    }
                    continue;
                }

                var chosenIndex = int.Parse(response.InteractionResponse.Data.CustomId);
                var chosenLetter = ((char)('A' + chosenIndex)).ToString();

                if (new List<string>() { quizLog.Guess1, quizLog.Guess2, quizLog.Guess3 }.Contains(choices[chosenIndex]))
                {
                    await channel.SendMessageAsync(DialogueDict.Get($"QUIZ_WRONG_REPEAT"));
                    continue;
                }

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

            var achievementId = quizLog.Correct ? $"QUIZ_CORRECT_{quizLog.Guesses}" : "QUIZ_PARTICIPATE";
            var achievement = await repo.GetAchievementAsync(connection, achievementId);

            embed = GetEmbed();
            embed.Title = correct
                ? $"✅ Kuh-rect!! ({new List<string>() { "Zeroth", "First", "Second", "Third" }[guesses]} Attempt)"
                : "❌ Incorrect";
            if (correct)
                embed.Description = DialogueDict.Get($"QUIZ_CORRECT_{guesses}", achievement.DefaultScore);
            else
                embed.Description = DialogueDict.Get($"QUIZ_INCORRECT", correctChoice, user.Queen(client), achievement.DefaultScore);
            embed.Description += "\n\n" + DialogueDict.Get("QUIZ_CLOSING");

            components = new ComponentBuilder()
                .WithButton("💬 Discuss Today's Quiz!", $"QUIZ.D:{day},{chosenQuizIndex}");

            await channel.SendMessageAsync(embed: embed.Build(), components: components.Build());
            await AddScoreAndFinish(connection, achievement, quizMessage);
        }

        async Task AddScoreAndFinish(SqlConnection connection, Achievement achievement, IMessage quizMessage)
        {

            var gyn = client.GetGyn(config);
            var discussionChannel = gyn.GetChannelFromConfig(config, "quizdiscussionchannel") as SocketTextChannel;
            var quizChannel = gyn.GetChannelFromConfig(config, "quizchannel") as SocketTextChannel;
            var quizTakenRole = gyn.GetRoleFromConfig(config, "quiztakenrole");
            var quizOpenedMessage = (await quizChannel.GetPinnedMessagesAsync()).FirstOrDefault();
            var quizOpenedUrl = quizOpenedMessage?.GetJumpUrl();

            var overridePoints = 0m;
            if (quizLog.Correct && user.IsGYNSage(config))
            {
                var pAchievement = await repo.GetAchievementAsync(connection, "QUIZ_PARTICIPATE");
                overridePoints = pAchievement.DefaultScore;
            }

            await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, client.CurrentUser, DateTime.Now, quizMessage, titleUrl: quizOpenedUrl, overridePoints: overridePoints);
            var previousLog = await repo.GetLastQuizLogForUserAsync(connection, user.Id.ToString(), quizLog.Day.ToString());
            // Streak bonus
            if (quizLog.Correct && !user.IsGYNSage(config) && quizLog.Guesses == 1 && quizLog.Day >= int.Parse(config["firstquizstreakday"]))
            {
                if (previousLog != null && previousLog.Correct && previousLog.Guesses == 1)
                {
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, "QUIZ_STREAK", client.CurrentUser, DateTime.Now, quizMessage, titleUrl: quizOpenedUrl);
                    var embed = GetEmbed()
                        .WithTitle("🔥 Streak!!")
                        .WithDescription(DialogueDict.Get("QUIZ_STREAK"));
                    await channel.SendMessageAsync(embed: embed.Build());
                }
            }

            //var guildUser = gyn.GetUser(user.Id);
            //if (guildUser != null && availableQuizzes != null && availableQuizzes.Any())
            //{
            //    await guildUser.AddRoleAsync(quizTakenRole);
            //    //var msgText = DialogueDict.Get("DAILY_QUIZ_DISCUSSION_WELCOME", previousLog == null ? user.Mention : (guildUser.Nickname ?? guildUser.Username));
            //    var msgText = DialogueDict.Get("DAILY_QUIZ_DISCUSSION_WELCOME", user.Mention);
            //    if (availableQuizzes.Count > 1)
            //        msgText += "\n" + DialogueDict.Get("DAILY_QUIZ_DISCUSSION_CHOICE", chosenQuizIndex + 1, availableQuizzes[chosenQuizIndex].Category);
            //    //if (previousLog == null)
            //    //    msgText += "\n" + DialogueDict.Get("DAILY_QUIZ_DISCUSSION_REMINDER");
            //    await discussionChannel.SendMessageAsync(msgText);//, allowedMentions: previousLog == null ? AllowedMentions.All : AllowedMentions.None);
            //}
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

                quizLog.Guesses = 3;
                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                await repo.UpdateQuizLogAsync(connection, quizLog);

                var achievement = await repo.GetAchievementAsync(connection, "QUIZ_PARTICIPATE");
                MarkCancelled(DialogueDict.Get("QUIZ_TIMEOUT", correctChoice, user.Queen(client), achievement.DefaultScore)
                    + "\n\n" + DialogueDict.Get("QUIZ_CLOSING"));

                await Task.Delay(500);
                await AddScoreAndFinish(connection, achievement, currentPrompt?.BotMessage);
            }
        }
    }
}
