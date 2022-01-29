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
using PrideBot.Events;
using PrideBot.Game;
using PrideBot.Quizzes;

namespace PrideBot.Modules
{
    [Name("Owner")]
    [RequireOwner]
    public class OwnerModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly AnnouncementService announcementService;
        readonly LeaderboardService leaderboardService;
        readonly IConfigurationRoot config;
        readonly LeaderboardImageGenerator leaderboardImageGenerator;
        readonly SceneDialogueService sceneDialogueService;
        readonly SnakeGame snakeGame;

        public OwnerModule(ModelRepository repo, AnnouncementService announcementService, LeaderboardService leaderboardService, IConfigurationRoot config, LeaderboardImageGenerator leaderboardImageGenerator, SceneDialogueService sceneDialogueService, SnakeGame snakeGame)
        {
            this.repo = repo;
            this.announcementService = announcementService;
            this.leaderboardService = leaderboardService;
            this.config = config;
            this.leaderboardImageGenerator = leaderboardImageGenerator;
            this.sceneDialogueService = sceneDialogueService;
            this.snakeGame = snakeGame;
        }

        [Command("announceintro")]
        [RequireContext(ContextType.Guild)]
        public async Task AnnounceIntro()
        {
            await announcementService.IntroAnnouncementAsync(Context.Guild);
        }

        [Command("announcestart")]
        [RequireContext(ContextType.Guild)]
        public async Task AnnounceStart()
        {
            await announcementService.StartAnnouncementAsync(Context.Guild);
        }
        [Command("yurikogif")]
        [Alias("createyurikogif")]
        public async Task CreateYurikoGifAsync(string frontUrl, string backUrl)
        {
            using var typing = Context.Channel.EnterTypingState();
            using var collection = await leaderboardImageGenerator.CreateYurikoGifAsync(frontUrl, backUrl);
            using var stream = (await collection.WriteToMemoryFileAsync("yuriko")).Stream;
            await Context.Channel.SendFileAsync(stream, "yuriko.gif");
        }

        [Command("refreshleaderboard")]
        [Alias("updateleaderboard")]
        public async Task RefreshLeaderboard()
        {
            using var typing = Context.Channel.EnterTypingState();
            await leaderboardService.UpdateLoaderboardAsync();
            await ReplyResultAsync("Done!");
        }

        [Command("runscene")]
        public async Task RunScene(string sceneId, IMessageChannel channel)
        {
            await sceneDialogueService.PerformCutscene(sceneId, channel);
        }

        [Command("tsuchiconnect")]
        public async Task TsuchiConect()
        {
            await snakeGame.SetLastSnakeDayAsync(GameHelper.GetEventDay() - 1);
            snakeGame.SetNextSnakeTime(DateTime.Now);
            await ReplyResultAsync("Done!");
        }
    }
}