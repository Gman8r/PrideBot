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

namespace PrideBot.Modules
{
    [Name("Owner")]
    [RequireOwner]
    [RequireContext(ContextType.DM)]
    public class OwnerModule : PrideModuleBase
    {

    }
}