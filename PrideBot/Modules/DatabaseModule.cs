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

namespace PrideBot.Modules
{
    [Name("Database")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireContext(ContextType.DM)]
    public class DatabaseModule : PrideModuleBase
    {
        readonly ModelRepository modelRepository;

        public DatabaseModule(ModelRepository modelRepository)
        {
            this.modelRepository = modelRepository;
        }
    }
}