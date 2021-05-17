using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Registration;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Game
{
    public class ScoringService
    {
        readonly ModelRepository repo;
        readonly DiscordSocketClient client;
        readonly ShipImageGenerator shipImageGenerator;
        readonly IConfigurationRoot config;

        public ScoringService(ModelRepository repo, DiscordSocketClient client, ShipImageGenerator shipImageGenerator, IConfigurationRoot config)
        {
            this.repo = repo;
            this.client = client;

            client.ReactionAdded += ReactionAddedAsync;
            this.shipImageGenerator = shipImageGenerator;
            this.config = config;
        }

        public async Task AddAndDisplayAchievementAsync(SqlConnection connection, ITextChannel channel, IUser user, Achievement achievement, IUser approver, int overridePoints = 0, string titleUrl = null, bool ignoreIfNotRegistered = true)
        {
            if (overridePoints > 999)
                throw new CommandException("Max score for an achievement is 999, WHY ARE YOU EVEN DOING THIS??");
            else if (overridePoints < 0)
                throw new CommandException("I can't reverse the threads of love! Score must be positive or 0 for default.");

            var dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            var pointsEarned = overridePoints == 0 ? achievement.DefaultScore : overridePoints;
            IEnumerable<Score> dbScores = null;
            if (!dbUser.ShipsSelected)
            {
                if (ignoreIfNotRegistered)
                    return;
            }
            else
            {
                var groupId = await repo.AddScoreAsync(connection, user.Id.ToString(), achievement.AchievementId, pointsEarned);
                dbScores = await repo.GetScoresFromGroupAsync(connection, groupId);
            }

            //if (achievement.Log)
            //{
                await DisplayAchievementAsync(channel, user, dbUser, achievement, dbScores,
                    await repo.GetUserShipsAsync(connection, user.Id.ToString()), approver, titleUrl);
            //}
        }

        public async Task DisplayAchievementAsync(ITextChannel channel, IUser user, User dbUser, Achievement achievement, IEnumerable<Score> dbScores,
            UserShipCollection dbUserShips, IUser approver, string titleUrl = null)
        {
            var embed = await GenerateAchievementEmbedAsync(user, dbUser, achievement, dbScores?.ToArray(), dbUserShips, approver, titleUrl);
            await channel.SendMessageAsync(user.Mention, embed: embed.Build());
        }

        public async Task<EmbedBuilder> GenerateAchievementEmbedAsync(IUser user, User dbUser, Achievement achievement, Score[] dbScores,
            UserShipCollection dbUserShips, IUser approver, string titleUrl = null)
        {
            var embed = EmbedHelper.GetEventEmbed(user, config, id: (dbScores?.FirstOrDefault()?.ScoreGroupId.ToString()) ?? "", showDate: true)
                .WithTitle($"{achievement.Emoji} Challenge Completed: {achievement.Description}!")
                .WithUrl(titleUrl)
                .WithDescription(achievement.Flavor);
            if (approver != null)
                embed.Footer.Text += $" | Approver: {approver.Username}#{approver.Discriminator}";

                if (dbUser.ShipsSelected && dbUserShips.Any())
            {
                var scoreStr = "";
                for (int i = 0; i < dbUserShips.Count(); i++)
                {
                    if (!dbUserShips.Has((UserShipTier)i))
                        continue;
                    var userShip = dbUserShips.Get((UserShipTier)i);
                    scoreStr += $"{EmoteHelper.GetShipTierEmoji((UserShipTier)userShip.Tier)} **{dbScores[i].PointsEarned}** for **{userShip.GetDisplayName()}**\n";
                }
                embed.AddField($"You've Earned {EmoteHelper.SPEmote} !", scoreStr);

                var scores = Enumerable.Range(0, 3)
                    .Select(a => dbScores
                        .FirstOrDefault(aa => ((int)aa.Tier) == a)?.PointsEarned ?? 0)
                    .ToArray();
                var imagePath = await shipImageGenerator.WriteUserAvatarAsync(dbUser, dbUserShips, scores: scores);
                embed.ImageUrl = config.GetRelativeHostPathWeb(imagePath);
            }
            else
            {
                embed.Description += $"\n\n{ DialogueDict.Get("ACHIEVEMENT_NOT_REGISTERED", achievement.DefaultScore, config.GetDefaultPrefix())}";
            }

            return embed;
        }

        private async Task ReactionAddedAsync(Cacheable<Discord.IUserMessage, ulong> msg, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                var gChannel = channel as SocketGuildChannel;
                if (gChannel == null)
                    return;
                var gUser = gChannel.Guild.GetUser(reaction.UserId);
                if (gUser == null || gUser.IsBot || !gUser.GuildPermissions.Has(GuildPermission.Administrator))
                    return;

                using var connection = DatabaseHelper.GetDatabaseConnection();
                await connection.OpenAsync();
                var achievement = await repo.GetAchievementFromEmojiAsync(connection, reaction.Emote.ToString());
                if (achievement == null)// || !achievement.Manual)
                    return;

                var message = await msg.GetOrDownloadAsync();
                if (message.Author.IsBot) return;
                await AddAndDisplayAchievementAsync(connection, channel as ITextChannel, message.Author, achievement,
                    reaction.User.IsSpecified ? reaction.User.Value : null, titleUrl: message.GetJumpUrl(), ignoreIfNotRegistered: false);

                await (message.AddReactionAsync(reaction.Emote));
            }
            catch (Exception e)
            {
                await channel.SendMessageAsync(e.Message);
                throw;
            }

        }
    }
}
