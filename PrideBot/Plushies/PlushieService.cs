﻿using Discord;
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
        readonly DiscordSocketClient client;

        public PlushieService(IConfigurationRoot config, ModelRepository repo, PlushieImageService imageService, DiscordSocketClient client)
        {
            this.config = config;
            this.repo = repo;
            this.imageService = imageService;
            this.client = client;
        }


        public async Task DrawPlushie(SqlConnection connection, IMessageChannel channel, SocketUser user, IDiscordInteraction interaction = null)
        {
            var session = new PlushieDrawSession(channel, user, config, repo, client, TimeSpan.FromMinutes(10), null, imageService, interaction);
            await session.PerformSessionAsync();
        }

    }
}
