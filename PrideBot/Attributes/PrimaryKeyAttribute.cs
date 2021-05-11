using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    class PrimaryKeyAttribute : Attribute
    {
    }
}
