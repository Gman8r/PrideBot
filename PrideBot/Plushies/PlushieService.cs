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
    public class PlushieService
    {

        readonly IConfigurationRoot config;
        readonly ModelRepository repo;
        readonly PlushieImageService imageService;

        public PlushieService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
        }

        public async Task DrawPlushie(SqlConnection connection, IMessageChannel channel, IDiscordInteraction interaction = null)
        {
            //var chr = await repo.GetCharacterAsync(connection, "REIMU");
            var rotation = -20m + (decimal)(new Random(DateTime.Now.Millisecond).NextDouble() * 40);
            //rotation *= 3;
            var userPlushie = new UserPlushie()
            {
                CharacterId = "REIMU",
                Rotation = rotation
            };
            Console.WriteLine(rotation);
            var imageFile = await imageService.WritePlushieImageAsync(userPlushie);
            if (interaction != null)
                await interaction.FollowupWithFileAsync(imageFile.Stream, imageFile.FileName);
            else
                await channel.SendFileAsync(imageFile.Stream, imageFile.FileName);
        }

    }
}
