using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrideBot.TypeReaders;
using PrideBot.Models;
using Microsoft.Data.SqlClient;
using PrideBot.Repository;
using RestSharp;
using Newtonsoft.Json;

namespace PrideBot
{
    public class PluralKitApiService
    {

        readonly IConfigurationRoot config;
        readonly TokenConfig tokenConfig;

        public PluralKitApiService(IConfigurationRoot config, TokenConfig tokenConfig)
        {
            this.config = config;
            this.tokenConfig = tokenConfig;
        }

        public async Task<PkMessage> GetPKMessageAsync(IMessage message)
        {
            var apiToken = tokenConfig["pluralkitapitoken"];
            var restClient = new RestClient("https://www.googleapis.com/youtube/v3/");
            var requestStr = $"https://api.pluralkit.me/v1/msg/{message.Id.ToString()}";
            var request = new RestRequest(requestStr, Method.GET);
            request.AddHeader("Authorization", apiToken);
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };

            try
            {
                var queryResult = await restClient.ExecuteAsync(request);
                if (!queryResult.Content.StartsWith("{") || queryResult.Content.StartsWith("}"))
                    return null;
                return JsonConvert.DeserializeObject<PkMessage>(queryResult.Content);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> IsMessageFromPkUserAsync(IMessage message)
        {
            if (message.Author.IsWebhook)
            {
                var gChannel = (message.Channel as IGuildChannel);
                if (gChannel == null)
                    return false;
                var hook = await gChannel.Guild.GetWebhookAsync((message.Author as IWebhookUser).WebhookId);
                return hook.Creator?.Id == ulong.Parse(config["ids:pluralkitid"]);
            }
            return false;
        }


        public async Task<IUser> GetUserOrPkUserAsync(SocketGuild guild, IMessage userMessage)
        {
            // Determine user or PK user
            IUser user;
            if (userMessage.Author.IsWebhook && await IsMessageFromPkUserAsync(userMessage))
            {
                var pkMessage = await GetPKMessageAsync(userMessage);
                user = pkMessage == null
                    ? null : guild.GetUser(ulong.Parse(pkMessage.sender));
            }
            else if (!userMessage.Author.IsBot)
            {
                user = userMessage.Author;
            }
            else
                user = null;
            return user;
        }
    }
}
