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

        public PlushieEffectService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
        }

        public async Task ActivatePlushie(SqlConnection connection, IGuildUser user, UserPlushie userPlushie, IMessageChannel channel, IDiscordInteraction interaction = null)
        {
            EmbedBuilder embed;
            switch(userPlushie.PlushieId)
            {
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
    }
}
