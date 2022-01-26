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
using PrideBot.Quizzes;
using PrideBot.Game;
using PrideBot.Registration;

namespace PrideBot.Plushies
{
    public partial class PlushieEffectService
    {
        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly PlushieImageService imageService;
        readonly DiscordSocketClient client;
        readonly ScoringService scoringService;

        public PlushieEffectService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client, ScoringService scoringService)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
            this.scoringService = scoringService;
        }

        public async Task ActivatePlushie(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction = null)
        {
            EmbedBuilder embed;
            switch(userPlushie.PlushieId)
            {
                case "ADVANCED_MATHEMATICS":
                    await AdvancedMathematicsAsync(connection, user, userPlushie, channel, interaction);
                    break;
                default:
                    await repo.ActivateUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now);
                    embed = EmbedHelper.GetEventEmbed(user, config)
                        .WithTitle("Activated!!")
                        .WithDescription(userPlushie.DurationHours > 0
                        ? DialogueDict.Get("PLUSHIE_ACTIVATED_DURATION", userPlushie.CharacterName, userPlushie.DurationHours)
                        : DialogueDict.Get("PLUSHIE_ACTIVATED_USES", userPlushie.CharacterName));
                    if (interaction == null)
                        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
                    else
                        await interaction.FollowupAsync(user.Mention, embed: embed.Build());
                    break;
            }
        }

        async Task AdvancedMathematicsAsync(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction)
        {
            var allShips = await repo.GetAllShipsAsync(connection);
            var userShips = await repo.GetUserShipsAsync(connection, user.Id.ToString());
            var supportedShips = allShips
                .Where(a => userShips.Any(aa => aa.ShipId == a.ShipId));

            var tutorial = "";
            var sum = supportedShips.Sum(a => (int)Math.Round(a.PointsEarned));
            if (supportedShips.Count() > 1)
                tutorial += string.Join("\n+ ", supportedShips.Select(a => $" **{(int)Math.Round(a.PointsEarned)}** from **{a.GetDisplayName()}**")) + "\n= ";
            else
                tutorial += $"Your only supported pairing (**{supportedShips.FirstOrDefault().GetDisplayName()}**) has ";

            var ones = sum % 10;
            var tens = ((sum - (sum % 10)) / 10) % 10;
            var hundredsPlus = (sum - (sum % 100)) / 100;
            if (hundredsPlus > 0)
                tutorial += hundredsPlus.ToString();
            tutorial += $"**{tens}{ones}** {EmoteHelper.SPEmote}.";

            var digitsSum = ones + tens;
            var pointMult = 5; // TODO db mult
            var pointsTotal = digitsSum * pointMult;
            tutorial += $"\n\n{ones} + {tens} = **{digitsSum}**";
            tutorial += "\n\n UHHHHH i'll figure out the mult later, let's just say x5. Also i think she should have different reactions depending on how high you get and for the low ones she basically makes fun of you lowkey.";

            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Okay! Leeeeet's see!")
                .WithThumbnailUrl(client.CurrentUser.GetServerAvatarUrlOrDefault())
                .WithDescription(tutorial);
            var msg = await interaction.FollowupAsync(embed: embed.Build());

            await scoringService.AddAndDisplayAchievementAsync(connection, user, "PLUSHIE_MATH", client.CurrentUser, DateTime.Now, msg, pointsTotal, msg.GetJumpUrl());
            await repo.DepleteUserPlushieAsync(connection, userPlushie.UserPlushieId, DateTime.Now, true, PlushieUseContext.Score, "TODO");
        }
    }
}
