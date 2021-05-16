using Discord.WebSocket;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Game
{
    public class ScoringService
    {
        readonly ModelRepository repo;
        readonly DiscordSocketClient client;

        public ScoringService(ModelRepository repo, DiscordSocketClient client)
        {
            this.repo = repo;
            this.client = client;

            client.ReactionAdded += ReactionAddedAsync;
        }

        public async Task AddAndDisplayScoreAsync(string userId, string achivementId, string achievementId)
        {

        }

        private async Task ReactionAddedAsync(Discord.Cacheable<Discord.IUserMessage, ulong> msg, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var gChannel = channel as SocketGuildChannel;
            if (gChannel == null) return;
            var gUser = gChannel.Guild.GetUser(reaction.UserId);
            if (gUser == null) return;
            //if (gChannel.GetUser(reaction.UserId).GuildPermissions)

        }
    }
}
