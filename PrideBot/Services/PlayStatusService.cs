using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace PrideBot
{
    public class PlayStatusService
    {
        private DateTime nextChange;
        private readonly IConfigurationRoot config;
        private readonly DiscordSocketClient client;

        public PlayStatusService(IConfigurationRoot config, DiscordSocketClient client)
        {
            this.config = config;
            this.client = client;

            client.Connected += Start;
        }

        private Task Start()
        {
            ChangeStatusLoop().GetAwaiter();
            return Task.CompletedTask;
        }

        public async Task NextStatusAsync()
        {
            var statuses = File.ReadAllLines(config.GetRelativeFilePath("statuses.txt"));
            await client.SetGameAsync(statuses[new Random().Next() % statuses.Length]);
            nextChange = DateTime.Now.AddMinutes(double.Parse(config["statusminutes"]));
        }

        private async Task ChangeStatusLoop()
        {
            await NextStatusAsync();
            while (true)
            {
                if (DateTime.Now >= nextChange)
                    await NextStatusAsync();
                await Task.Delay(1000);
            }

        }
    }
}
