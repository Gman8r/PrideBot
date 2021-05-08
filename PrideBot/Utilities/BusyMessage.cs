using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public class BusyMessage : IDisposable
    {

        bool disposed;
        IMessage message;

        public BusyMessage(IMessageChannel channel, string text)
        {
            CreateMessageAsync(channel, text).GetAwaiter();
        }

        public async Task CreateMessageAsync(IMessageChannel channel, string text)
        {
            message = await channel.SendMessageAsync(ProgressBar.HaniwaEmote + " " + text);
            if (disposed)
            {
                await AttemptDeleteAsync();
            }
        }
    
        public void Dispose()
        {
            disposed = true;
            AttemptDeleteAsync().GetAwaiter();
        }

        public async Task AttemptDeleteAsync()
        {
            try
            {
                await message.DeleteAsync();
            }
            catch
            {

            }
        }
    }
}
