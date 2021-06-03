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
        readonly LeaderboardImageGenerator leaderboardImageGenerator;
        readonly IConfigurationRoot config;

        public OwnerModule(ModelRepository repo, AnnouncementService announcementService, LeaderboardImageGenerator leaderboardImageGenerator, IConfigurationRoot config)
        {
            this.repo = repo;
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
            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();
            var allShips = await repo.GetAllActiveShipsAsync(connection);

            var topShips = allShips
                .OrderByDescending(a => a.PointsEarned)
                .ThenByDescending(a => a.Supporters)
                .ToList();
            var topRareShips = topShips
                .Where(a => a.Supporters <= 5)
                .ToList();

            var imagePath = await leaderboardImageGenerator.WriteLeaderboardAsync(topShips, topRareShips);
            var embed = EmbedHelper.GetEventEmbed(Context.User, config)
                .WithDescription("OMGGG Top secret mod data... how's it look besties??")
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath));
            embed.Fields = new List<EmbedFieldBuilder>();
            embed.Fields.AddRange(GetEmbedFieldsForLeaderboard(topShips, "Champions of Love:"));
            embed.Fields.AddRange(GetEmbedFieldsForLeaderboard(topRareShips, "Our Beloved Underdogs:"));
            await ReplyAsync(embed: embed.Build());
        }

        List<EmbedFieldBuilder> GetEmbedFieldsForLeaderboard(List<Ship> ships, string name)
        {
            var namesList = ships.Select(a => 
                $"__#{ships.IndexOf(a) + 1}: **{a.GetDisplayName()}**__\n**{a.PointsEarned} {EmoteHelper.SPEmote}**\n").ToList();
            var maxLength = 10;
            var lengths = new int[1];
            lengths[0] = Math.Min((int)Math.Ceiling((double)namesList.Count / 1.0), maxLength);
            //lengths[1] = Math.Min((int)Math.Ceiling((double)namesList.Count / 3.0), maxLength);
            //lengths[2] = Math.Min(namesList.Count - lengths[0] - lengths[1], maxLength);
            var fields = new List<EmbedFieldBuilder>();
            for (int i = 0; i < lengths.Length; i++)
            {
                var field = new EmbedFieldBuilder()
                    .WithName("\u200B")
                    .WithValue(string.Join("\n", namesList.Take(lengths[i])))
                    .WithIsInline(true);
                namesList = namesList.Skip(lengths[i]).ToList();
                fields.Add(field);
            }
            fields[0].Name = name;
            return fields;
        }
    }
}