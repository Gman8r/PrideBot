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

        public OwnerModule(ModelRepository repo, AnnouncementService announcementService, LeaderboardService leaderboardService, IConfigurationRoot config)
        {
            this.repo = repo;
            this.announcementService = announcementService;
            this.leaderboardService = leaderboardService;
            this.config = config;
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

        [Command("refreshleaderboard")]
        [Alias("updateleaderboard")]
        public async Task RefreshLeaderboard()
        {
            using var typing = Context.Channel.EnterTypingState();
            await leaderboardService.UpdateLoaderboardAsync();
            await ReplyResultAsync("Done!");
        }
    }
}