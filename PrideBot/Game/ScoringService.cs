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
        readonly LoggingService loggingService;

        public ScoringService(ModelRepository repo, DiscordSocketClient client, ShipImageGenerator shipImageGenerator, IConfigurationRoot config, LoggingService loggingService)
        {
            this.repo = repo;
            this.client = client;
            this.loggingService = loggingService;

            client.ReactionAdded += ReactionAddedAsync;
            this.shipImageGenerator = shipImageGenerator;
            this.config = config;
        }

        public async Task<bool> AddAndDisplayAchievementAsync(SqlConnection connection, IUser user, string achievementId, IUser approver, int overridePoints = 0, string titleUrl = null, bool ignoreIfNotRegistered = false, ITextChannel overrideChannel = null, bool ignoreCooldown = false)
        {
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            return await AddAndDisplayAchievementAsync(connection, user, achievement, approver, overridePoints, titleUrl, ignoreIfNotRegistered, overrideChannel, ignoreCooldown);
        }

        // Returns True if score was applied in some way
        public async Task<bool> AddAndDisplayAchievementAsync(SqlConnection connection, IUser user, Achievement achievement, IUser approver, int overridePoints = 0, string titleUrl = null, bool ignoreIfNotRegistered = false, ITextChannel overrideChannel = null, bool ignoreCooldown  = false)
        {
            if (overridePoints > 999)
                throw new CommandException("Max score for an achievement is 999, WHY ARE YOU EVEN DOING THIS??");
            else if (overridePoints < 0)
                throw new CommandException("I can't reverse the threads of love! Score must be positive or 0 for default.");

            overrideChannel ??= client.GetGyn(config)
                .GetChannelFromConfig(config, "achievementschannel") as ITextChannel;

            var dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            var pointsEarned = overridePoints == 0 ? achievement.DefaultScore : overridePoints;

            var addScoreResults = await repo.AttemptAddScoreAsync(connection, user.Id.ToString(), achievement.AchievementId, pointsEarned, approver.Id.ToString(), ignoreCooldown);
            var scoreId = addScoreResults.Item1;
            var errorCode = addScoreResults.Item2;

            if (achievement.Log)
            {
                var dbShipScores = (await repo.GetShipScoresAsync(connection, scoreId)).ToArray();

                var embed = await GenerateAchievementEmbedAsync(user, dbUser, achievement, scoreId, pointsEarned, dbShipScores,                    await repo.GetUserShipsAsync(connection, user.Id.ToString()), approver, titleUrl, errorCode);
                var text = user.Mention;
                if (!achievement.Ping || !dbUser.PingForAchievements)
                {
                    text = null;
                    embed.Author ??= new EmbedAuthorBuilder();
                    embed.Author.Name = $"{user.Username}#{user.Discriminator}";
                }

                await overrideChannel.SendMessageAsync(text, embed: embed.Build());
            }
            return errorCode == ModelRepository.AddScoreError.None || errorCode == ModelRepository.AddScoreError.UserNotRegistered;
        }

        public async Task<EmbedBuilder> GenerateAchievementEmbedAsync(IUser user, User dbUser, Achievement achievement, string scoreId, int basePointsEarned, ShipScore[] dbShipScores, UserShipCollection dbUserShips, IUser approver,
            string titleUrl = null, ModelRepository.AddScoreError errorCode = ModelRepository.AddScoreError.None)
        {
            var embed = EmbedHelper.GetEventEmbed(user, config, id: scoreId, showDate: true, userInThumbnail: true)
                .WithTitle($"{achievement.Emoji} Challenge Completed: {achievement.Description}!")
                .WithDescription(DialogueDict.RollBullshit(achievement.Flavor))
                .WithUrl(titleUrl);
            if (approver != null)
                embed.Footer.Text += $" | Approver: {approver.Username}#{approver.Discriminator}";

            switch (errorCode)
            {
                case (ModelRepository.AddScoreError.None):
                    var scoreStr = "";
                    foreach (var shipScore in dbShipScores)
                    {
                        var userShip = dbUserShips.Get((UserShipTier)shipScore.Tier);
                        scoreStr += $"{EmoteHelper.GetShipTierEmoji((UserShipTier)userShip.Tier)} **{shipScore.PointsEarned}** for **{userShip.GetDisplayName()}**\n";
                    }
                    embed.AddField($"You feel your bond with your community grow stronger...\nYou've earned {EmoteHelper.SPEmote} !", scoreStr.Trim());

                    var scores = Enumerable.Range(0, 3)
                        .Select(a => dbShipScores
                            .FirstOrDefault(aa => ((int)aa.Tier) == a)?.PointsEarned ?? 0)
                        .ToArray();
                    var imagePath = await shipImageGenerator.WriteUserCardAsync(dbUser, dbUserShips, scores: scores);
                    embed.ImageUrl = config.GetRelativeHostPathWeb(imagePath);
                    break;

                case (ModelRepository.AddScoreError.UserNotRegistered):
                    embed.Description += $"\n\n{ DialogueDict.Get("ACHIEVEMENT_NOT_REGISTERED", basePointsEarned, config.GetDefaultPrefix())}";
                    break;

                case (ModelRepository.AddScoreError.CooldownViolated):
                    embed.Description = DialogueDict.Get("ACHIEVEMENT_COOLDOWN_VIOLATED", achievement.CooldownHours);
                    break;

                default:
                    break;
            }

            return embed;
        }

        private async Task ReactionAddedAsync(Cacheable<Discord.IUserMessage, ulong> msg, ISocketMessageChannel channel, SocketReaction reaction)
            => CheckReactionForScore(msg, channel, reaction).GetAwaiter();

        private async Task CheckReactionForScore(Cacheable<Discord.IUserMessage, ulong> msg, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                var gChannel = channel as SocketGuildChannel;
                if (gChannel == null || !gChannel.Guild.IsGyn(config))
                    return;
                var gUser = gChannel.Guild.GetUser(reaction.UserId);
                if (gUser == null || gUser.IsBot || !gUser.IsGYNSage(config))
                    return;

                using var connection = DatabaseHelper.GetDatabaseConnection();
                await connection.OpenAsync();
                var achievement = await repo.GetAchievementFromEmojiAsync(connection, reaction.Emote.ToString());
                if (achievement == null || !achievement.Manual)
                    return;

                var message = await msg.GetOrDownloadAsync();
                if (message.Reactions[reaction.Emote].IsMe) return;

                // Determine user or PK uer
                IUser user;
                if (message.Author.IsWebhook && await message.IsFromPkUserAsync(config))
                {
                    user = await message.GetPkUserAsync(config);
                }
                else if (!message.Author.IsBot)
                {
                    user = message.Author;
                }
                else
                    user = null;
                if (user == null) return;


                await AddAndDisplayAchievementAsync(connection, user, achievement,
                    reaction.User.IsSpecified ? reaction.User.Value : null, titleUrl: message.GetJumpUrl(), ignoreIfNotRegistered: false);

                await (message.AddReactionAsync(reaction.Emote));
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, GetType().Name, e.Message, e));
                throw;
            }

        }
    }
}
