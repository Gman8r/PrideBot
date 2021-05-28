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
using System.Diagnostics;
using Newtonsoft.Json;
using PrideBot.Repository;
using PrideBot.Registration;
using PrideBot.Models;
using PrideBot.Game;
using PrideBot.Quizzes;
using PrideBot.Events;

namespace PrideBot.Modules
{
    [Name("Admin")]
    [RequireSage]
    public class AdminModule : PrideModuleBase
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly ShipImageGenerator shipImageGenerator;
        private readonly DiscordSocketClient client;
        private readonly ScoringService scoringService;
        readonly AnnouncementService announcementService;


        public AdminModule(CommandService service, IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, ShipImageGenerator shipImageGenerator, DiscordSocketClient client, ScoringService scoringService, AnnouncementService announcementService)
        {
            this.service = service;
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
            this.scoringService = scoringService;
            this.announcementService = announcementService;
        }

        [Command("giveachievement")]
        [Alias("grantachievement")]
        [Summary("Gives a user an achievement, same as adding reactions.")]
        [RequireGyn]
        [RequireContext(ContextType.Guild)]
        public async Task GiveAchievement(IUser user, string achievementId, bool ignoreCooldown = false, [DefaultValueName("achievement default")] int score = 0)
        {
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            if (achievement == null)
                throw new CommandException("Nope nuh-uh, I couldn't find that achievement like Anywheeeere, sorry! Make sure the Achievement Id matches the one in the sheet!");
            await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, Context.User, score, ignoreCooldown: ignoreCooldown);
            //if (Context.Client.GetGyn(config).GetChannelfromConfig(config, "achievementschannel").Id != Context.Channel.Id)
            await ReplyResultAsync("Done!");
        }

        [Command("revokeachievement")]
        [Alias("removeachievement")]
        [Summary("Removes a given achievement via its scoreboard message.")]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveAchievement(MessageUrl achievementdMessageUrl)
        {
            var message = achievementdMessageUrl.Value;
            var footerText = message.Embeds.FirstOrDefault()?.Footer?.Text;
            if (footerText == null)
                throw new CommandException("Noooope you gotta link to a valid achievement post from the achievement board.");
            int groupId;
            var groupIdStr = footerText.Split().FirstOrDefault() ?? "INVALID";
            if (!int.TryParse(groupIdStr, out groupId))
                throw new CommandException("Noooope you gotta link to a valid achievement post from the achievement board.");

            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var result = await repo.DeleteScoreAsync(connection, groupId.ToString());
            if (result <= 0)
                throw new CommandException("HMMMM sorry bestie, I couldn't find the achievement from that message. Did it get removed already?");

            await ReplyResultAsync("Daaaamn OK then, I have reversed the waves of love (just for a bit) and revoked the achievement!");
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(0)]
        [Summary("Relays a message.")]
        public async Task Echo([Remainder] string message)
        {
            await ReplyAsync(DialogueDict.RollBullshit(message));
        }

        [Command("say")]
        [Alias("echo")]
        [Priority(1)]
        [Summary("Relays a message in the specified chat channel.")]
        public async Task Echo(SocketTextChannel channel, [Remainder] string message)
        {
            await channel.SendMessageAsync(DialogueDict.RollBullshit(message));
            await ReplyResultAsync("Done!");
        }
    }
}