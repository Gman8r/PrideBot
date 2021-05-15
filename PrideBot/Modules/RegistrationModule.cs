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
using PrideBot.Registration;

namespace PrideBot.Modules
{
    [Name("Registration")]
    public class RegistrationModule : PrideModuleBase
    {
        readonly ModelRepository repo;
        readonly IConfigurationRoot config;
        readonly ShipImageGenerator shipImageGenerator;
        readonly DiscordSocketClient client;

        public RegistrationModule(ModelRepository modelRepository, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, DiscordSocketClient client)
        {
            this.repo = modelRepository;
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
            this.client = client;
        }
    }
}