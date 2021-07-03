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

namespace PrideBot.Game
{
    public class LeaderboardService
    {
        readonly ModelRepository repo;
        readonly LeaderboardImageGenerator leaderboardImageGenerator;
        readonly IConfigurationRoot config;
        readonly DiscordSocketClient client;
        readonly LoggingService loggingService;

        public LeaderboardService(ModelRepository repo, LeaderboardImageGenerator leaderboardImageGenerator, IConfigurationRoot config, DiscordSocketClient client, LoggingService loggingService)
        {
            this.repo = repo;
            this.leaderboardImageGenerator = leaderboardImageGenerator;
            this.config = config;
            this.client = client;
            this.loggingService = loggingService;

            client.Ready += DoLeaderboardLoop;
        }

        int GetHour(DateTime dt) => (dt.DayOfYear * 24)+ dt.Hour;
        int GetHour() => GetHour(DateTime.Now);

        private Task DoLeaderboardLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (GameHelper.GetEventPeriod(config) == EventPeriod.BeforeEvent)
                    {
                        await Task.Delay(6000);
                    }

                    var hour = GetHour();
                    if (DateTime.Now.Minute >= 55)
                        hour++;
                    if (GameHelper.IsEventOccuring(config))
                        await UpdateLoaderboardAsync();

                    while (GameHelper.IsEventOccuring(config))
                    {
                        while(GetHour(DateTime.Now.AddSeconds(30)) <= hour)
                        {
                            await Task.Delay(1000);
                        }
                        hour++;
                        await UpdateLoaderboardAsync();
                    }
                }
                catch (Exception e)
                {
                    await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                    var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false)
                        .WithTitle($"Exception in {this.GetType().Name} Module");
                    var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                    await modChannel.SendMessageAsync(embed: embed.Build());
                    throw e;
                }
                finally
                {
                    if (GameHelper.IsEventOccuring(config))
                    {
                        var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                        await modChannel.SendMessageAsync("The module will be attempt to re-enable in 30 minutes.");
                        await Task.Delay(30 * 60000);
                        DoLeaderboardLoop().GetAwaiter();
                    }
                }

            }).GetAwaiter();
            return Task.CompletedTask;
        }

        public async Task UpdateLoaderboardAsync()
        {
            var channel = client.GetGyn(config).GetChannelFromConfig(config, "leaderboardchannel") as IMessageChannel;
            var msg = (await channel.GetMessagesAsync().FlattenAsync())
                .FirstOrDefault(a => a.Author.Id == client.CurrentUser.Id
                    && !string.IsNullOrWhiteSpace(a.Embeds?.FirstOrDefault()?.Image?.Url));
            var embed = await GenerateLeaderboardEmbedAsync();
            if (msg != null)
                await (msg as IUserMessage).ModifyAsync(a => a.Embed = embed.Build());
            else
                await channel.SendMessageAsync(embed: embed.Build());
        }

        public async Task<EmbedBuilder> GenerateLeaderboardEmbedAsync()
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

            var imagePath = await leaderboardImageGenerator.WriteLeaderboardImageAsync(topShips, topRareShips);
            var embed = EmbedHelper.GetEventEmbed(null, config)
                .WithDescription(DialogueDict.Get("LEADERBOARD_DESCRIPTION"))
                .WithTitle("**The red string connects us all!**")
                .WithImageUrl(config.GetRelativeHostPathWeb(imagePath));
            embed.Fields = new List<EmbedFieldBuilder>();
            embed.Fields.AddRange(GetEmbedFieldsForLeaderboard(topShips, "Champions of Love:"));
            embed.Fields.AddRange(GetEmbedFieldsForLeaderboard(topRareShips, "Our Beloved Underdogs:"));

            return embed;
        }

        public string GetShipPlacementString(Ship ship, int place, bool includeTopContributor = false)
            => $"__#{place}: **{ship.GetDisplayName()}**__"
            + $"\n**{ship.PointsEarned} {EmoteHelper.SPEmote}**"
            + ((includeTopContributor && !string.IsNullOrEmpty(ship.TopSupporter))
                ? $" ({client.GetGyn(config).GetUser(ulong.Parse(ship.TopSupporter))?.Mention ?? "Unknown User"})"
            : "");

        public List<EmbedFieldBuilder> GetEmbedFieldsForLeaderboard(List<Ship> ships, string name, bool includeTopContributor = false)
        {
            var namesList = ships.Select(a =>
                GetShipPlacementString(a, ships.IndexOf(a) + 1, includeTopContributor) + "\n").ToList();
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
