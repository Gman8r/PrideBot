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
        readonly IServiceProvider provider;

        public ScoringService(ModelRepository repo, DiscordSocketClient client, ShipImageGenerator shipImageGenerator, IConfigurationRoot config, LoggingService loggingService, IServiceProvider provider)
        {
            this.repo = repo;
            this.client = client;
            this.loggingService = loggingService;

            client.ReactionAdded += ReactionAddedAsync;
            this.shipImageGenerator = shipImageGenerator;
            this.config = config;
            this.provider = provider;
        }

        string GetCooldownRemainingStr(DateTime cooldownExpires)
        {
            var ts = cooldownExpires - DateTime.Now;
            return $"{((int)ts.TotalHours).ToString("D2")}:{ts.Minutes.ToString("D2")}:{ts.Seconds.ToString("D2")}";
        }

        public async Task<ModelRepository.AddScoreError> AddAndDisplayAchievementAsync(SqlConnection connection, IUser user, string achievementId, IUser approver, int overridePoints = 0, string titleUrl = null, bool ignoreIfNotRegistered = false, bool ignoreCooldown = false, bool dontPing = false, IMessageChannel reportChannel = null)
        {
            var achievement = await repo.GetAchievementAsync(connection, achievementId);
            return await AddAndDisplayAchievementAsync(connection, user, achievement, approver, overridePoints, titleUrl, ignoreIfNotRegistered, ignoreCooldown, dontPing, reportChannel);
        }

        // Returns True if score was applied in some way
        public async Task<ModelRepository.AddScoreError> AddAndDisplayAchievementAsync(SqlConnection connection, IUser user, Achievement achievement, IUser approver, int overridePoints = 0, string titleUrl = null, bool ignoreIfNotRegistered = false, bool ignoreCooldown = false, bool dontPing = false, IMessageChannel reportChannel = null)
        {
            if (overridePoints > 999)
                throw new CommandException("Max score for an achievement is 999, WHY ARE YOU EVEN DOING THIS??");
            else if (overridePoints < 0)
                throw new CommandException("I can't reverse the threads of love! Score must be positive or 0 for default.");

            var achievementChannel = client.GetGyn(config)
                .GetChannelFromConfig(config, "achievementschannel") as ITextChannel;;

            var dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            var pointsEarned = overridePoints == 0 ? achievement.DefaultScore : overridePoints;

            var dbResult = await repo.AttemptAddScoreAsync(connection, user.Id.ToString(), achievement.AchievementId, pointsEarned, approver.Id.ToString(), ignoreCooldown);
            var scoreId = dbResult.ScoreId;
            var errorCode = dbResult.errorCode;
            var cooldownExpires = dbResult.CooldownExpires;

            if (achievement.Log)
            {
                var dbShipScores = (await repo.GetShipScoresAsync(connection, scoreId)).ToArray();

                var embed = await GenerateAchievementEmbedAsync(user, dbUser, achievement, scoreId, pointsEarned, dbShipScores,                    await repo.GetUserShipsAsync(connection, user.Id.ToString()), approver, titleUrl, dbResult);
                var text = user.Mention + " Achievement! " + achievement.Emoji;// + " Achievement!";// + " " + achievement.Emoji;

                if (errorCode == ModelRepository.AddScoreError.CooldownViolated)
                    text = user.Mention + ((user is SocketUser sUser) ? $" Hold Up {sUser.Queen(client)}!" : " Hold Up!");
                if (dontPing || !achievement.Ping || !dbUser.PingForAchievements)
                {
                    text = null;
                }
                else
                    embed.Item1.Author = null;

                var channel = client.GetGyn(config)
                        .GetChannelFromConfig(config, "achievementschannel") as IMessageChannel;
                if (reportChannel != null && errorCode == ModelRepository.AddScoreError.CooldownViolated)
                    channel = reportChannel;
                var post = embed.Item2 == null
                    ? await channel.SendMessageAsync(text, embed: embed.Item1.Build())
                    : await channel.SendFileAsync(embed.Item2.Stream, embed.Item2.FileName, text, embed: embed.Item1.Build());

                // Update database score with post data
                if (!string.IsNullOrEmpty(scoreId) && int.Parse(scoreId) > 0)
                {
                    var score = await repo.GetScoreAsync(connection, scoreId);
                    score.PostGuildId = (post.Channel as IGuildChannel)?.Guild.Id.ToString();
                    score.PostChannelId = post.Channel?.Id.ToString();
                    score.PostMessageId = post.Id.ToString();
                    await repo.UpdateScoreAsync(connection, score);
                }
            }

            if (approver != null && errorCode != ModelRepository.AddScoreError.CooldownViolated && achievement.AchievementId.ToLower().StartsWith("1cc"))
            {
                try
                {
                    await (await approver.CreateDMChannelAsync()).SendMessageAsync("Heyyy! Just making sure you fill in the value on the 1CC table for that achievement, 'kayyy?\n https://docs.google.com/spreadsheets/d/1vdAQ1QvBsuJViY8pftYxZXfEP8HWkCzzIf0IhSJCJcY/edit#gid=0");
                }
                catch
                {

                }
            }

            //if (errorCode == ModelRepository.AddScoreError.CooldownViolated && reportChannel != null && approver != null)
            //    await reportChannel.SendMessageAsync(embed: EmbedHelper.GetEventErrorEmbed(approver, $"OH NO Yikes I like, couldn't let that go through because it would voilate the cosmic cooldown, way sorrryyy. It'll be good to go again in **{GetCooldownRemainingStr(cooldownExpires)}**!", client).Build());
            return errorCode;
        }

        // returns (embed, ship image file)
        public async Task<(EmbedBuilder, MemoryFile)> GenerateAchievementEmbedAsync(IUser user, User dbUser, Achievement achievement, string scoreId, int basePointsEarned, ShipScore[] dbShipScores, UserShipCollection dbUserShips, IUser approver,
            string titleUrl = null, ModelRepository.AddScoreResult result = null)
        {
            var errorCode = result?.errorCode ?? ModelRepository.AddScoreError.None;
            var embed = EmbedHelper.GetEventEmbed(user, config, id: scoreId, showDate: true, userInThumbnail: true)
                .WithTitle($"{achievement.Emoji} {(errorCode == ModelRepository.AddScoreError.CooldownViolated ? "Cooldown Required" : "Challenge Completed")}" +
                $": {achievement.Description}!")
                .WithDescription(DialogueDict.GenerateEmojiText(achievement.Flavor))
                .WithUrl(titleUrl);
            if (approver != null)
                embed.Footer.Text += $" | {user.Id} | Approver: {approver.Username}#{approver.Discriminator} ({approver.Id})";

            MemoryFile imageFile = null;
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
                        .Select(a => "+" + (dbShipScores
                            .FirstOrDefault(aa => ((int)aa.Tier) == a)?.PointsEarned ?? 0).ToString())
                        .ToArray();
                    imageFile = await shipImageGenerator.WriteUserCardAsync(dbUser, dbUserShips, scoreTexts: scores);
                    embed.WithAttachedImageUrl(imageFile);
                    break;

                case (ModelRepository.AddScoreError.UserNotRegistered):
                    embed.Description += $"\n\n{ DialogueDict.Get("ACHIEVEMENT_NOT_REGISTERED", basePointsEarned, config.GetDefaultPrefix(), (client.GetGyn(config).GetChannelFromConfig(config, "ruleschannel") as ITextChannel).Mention)}";
                    break;

                case (ModelRepository.AddScoreError.CooldownViolated):
                    embed.Description = DialogueDict.Get("ACHIEVEMENT_COOLDOWN_VIOLATED", achievement.CooldownHours, GetCooldownRemainingStr(result.CooldownExpires));
                    break;

                default:
                    break;
            }

            return (embed, imageFile);
        }

        private async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chnl, SocketReaction reaction)
            => CheckReactionForScore(msg, chnl, reaction).GetAwaiter();

        private async Task CheckReactionForScore(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chnl, SocketReaction reaction)
        {
            try
            {
                var gChannel = client.Guilds
                    .SelectMany(a => a.TextChannels)
                    .FirstOrDefault(a => a.Id == chnl.Id);
                if (gChannel == null || !gChannel.Guild.IsGyn(config))
                    return;
                var gUser = gChannel.Guild.GetUser(reaction.UserId);
                if (gUser == null || gUser.IsBot || !gUser.IsGYNSage(config))
                    return;
                if (!GameHelper.IsEventOccuring(config))
                    return;

                using var connection = repo.GetDatabaseConnection();
                await connection.OpenAsync();
                var achievement = await repo.GetAchievementFromEmojiAsync(connection, reaction.Emote.ToString());
                if (achievement == null || !achievement.Manual)
                    return;

                var message = await msg.GetOrDownloadAsync();
                //if (message.Reactions[reaction.Emote].IsMe)
                //    return;

                // Determine user or PK user
                IUser user;
                if (message.Author.IsWebhook && await message.IsFromPkUserAsync(config))
                {
                    user = await message.GetPkUserAsync(config, provider);
                }
                else if (!message.Author.IsBot)
                {
                    user = message.Author;
                }
                else
                    user = null;
                if (user == null) return;


                await AddAndDisplayAchievementAsync(connection, user, achievement,
                    reaction.User.IsSpecified ? reaction.User.Value : null, titleUrl: message.GetJumpUrl(), ignoreIfNotRegistered: false,
                    reportChannel: message.Channel);

                await (message.AddReactionAsync(reaction.Emote));
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

        }
    }
}
