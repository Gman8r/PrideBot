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
        readonly ModelRepository modelRepository;
        readonly AnnouncementService announcementService;
        readonly LeaderboardImageGenerator leaderboardImageGenerator;
        readonly IConfigurationRoot config;

        public OwnerModule(ModelRepository modelRepository, AnnouncementService announcementService, LeaderboardImageGenerator leaderboardImageGenerator, IConfigurationRoot config)
        {
            this.modelRepository = modelRepository;
            this.announcementService = announcementService;
            this.leaderboardImageGenerator = leaderboardImageGenerator;
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

        [Command("leaderboard")]
        public async Task Leaderboard()
        {
            var imagePath = await leaderboardImageGenerator.WriteLeaderboardAsync();
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath));
            await ReplyAsync(embed: embed.Build());
        }
    }
}