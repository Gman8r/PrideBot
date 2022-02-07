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
using PrideBot.Repository;

namespace PrideBot
{
    class DatabaseJobService
    {
        private readonly DiscordSocketClient client;
        private readonly ModelRepository repo;
        private readonly LoggingService loggingService;

        public DatabaseJobService(DiscordSocketClient client, ModelRepository repo, LoggingService loggingService)
        {
            this.client = client;
            this.repo = repo;
            this.loggingService = loggingService;

            client.Ready += Ready;
        }

        private Task Ready()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await UpdateShipRecords();
                    var hour = DateTime.Now.Hour;
                    while (DateTime.Now.Hour == hour)
                    {
                        await Task.Delay(10 * 1000);
                    }
                }
            });
            return Task.CompletedTask;
        }

        async Task UpdateShipRecords()
        {
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            await repo.UpdateShipRecordsAsync(connection);
            await loggingService.OnLogAsync(new LogMessage(LogSeverity.Info, this.GetType().Name, "Update Ship Records"));
        }
    }
}
