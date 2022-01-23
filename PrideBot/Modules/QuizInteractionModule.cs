using Discord;
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
using System.Diagnostics;
using Newtonsoft.Json;
using PrideBot.Repository;
using PrideBot.Registration;
using PrideBot.Models;
using PrideBot.Game;
using PrideBot.Quizzes;
using PrideBot.Events;
using Discord.Interactions;
using Microsoft.Data.SqlClient;

namespace PrideBot.Modules
{
    public class QuizInteractionModule : InteractionModuleBase
    {
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly DiscordSocketClient client;

        public QuizInteractionModule(IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, DiscordSocketClient client)
        {
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.client = client;
        }

        [ComponentInteraction("QUIZ.D:*,*")]
        public async Task QuizDiscussButton(string dayStr, string quizIndexStr)
        {
            await DeferAsync();
            var day = int.Parse(dayStr);

            var gyn = client.GetGyn(config);
            var channel = gyn.GetChannelFromConfig(config, "quizchannel");
            var threads = gyn.ThreadChannels.Where(a => a.ParentChannel.Id == channel.Id);
            var discussionThread = threads
                .FirstOrDefault(a => a.Name.Equals($"Quiz Discussion Day {day}"));

            var threadUrl = $"https://discord.com/channels/{gyn.Id}/{discussionThread.Id}";
            if (discussionThread == null)
                throw new CommandException("I couldn't find a discussion thread for this quiz. Hmmmm, strange indeed.... Contact someone maybe!", ephemeral: true);
            var users = await discussionThread.GetUsersAsync();
            if (users.Any(a => a.Id == Context.User.Id))
                throw new CommandException($"You're already in the discussion thread! C'mon it's [right here]({threadUrl}), remember?", ephemeral: true);

            var quizIndex = int.Parse(quizIndexStr);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var quizzes = (await repo.GetQuizzesForDayAsync(connection, dayStr))
                .ToList();

            var msgText = DialogueDict.Get("DAILY_QUIZ_DISCUSSION_WELCOME", Context.User.Mention);
            if (quizzes.Count > 1 && quizIndex >= 0)
                msgText += "\n" + DialogueDict.Get("DAILY_QUIZ_DISCUSSION_CHOICE", quizIndex + 1, quizzes[quizIndex].Category);
            await discussionThread.SendMessageAsync(msgText);

            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithTitle("Discuss!")
                .WithDescription(DialogueDict.Get("QUIZ_DISCUSS_INVITE", threadUrl));
            await Context.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}