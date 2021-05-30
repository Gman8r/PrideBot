using ImageMagick;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Registration
{
    public class LeaderboardImageGenerator
    {
        readonly IConfigurationRoot config;

        public LeaderboardImageGenerator(IConfigurationRoot config)
        {
            this.config = config;
        }

        public string GetShipAvatarPath(User user, IConfigurationRoot config)
            => config.GetRelativeHostPathLocal("ships/" + user.UserId + ".png");

        public async Task<string> WriteLeaderboardAsync()
        {
            var path =
                await (await GenerateLeaderboardAsync())
                .WriteToWebFileAsync(config, "leaderboard");
            return path;
        }

        public async Task<MagickImageCollection> GenerateLeaderboardAsync()
        {
            return await GenerateBackgroundGifAsync();
        }

        class Star
        {
            public int x;
            public int y;
        }

        public async Task<MagickImageCollection> GenerateBackgroundGifAsync()
        {
            var rand = new Random();
            var width = 1920;
            var height = 1080;
            var bgColor = new MagickColor("#0D0F27");
            var starRows = 10;
            var starColumns = 20;
            var xPerColumn = (width / starColumns) + (width / 2);
            var yPerRow = (height / starRows) + (height / 2);
            var randomRange = 10;

            var stars = new List<Star>();
            for (int column = 0; column < starColumns; column++)
            {
                for (int row = 0; row < starRows; row++)
                {
                    int x = (column * xPerColumn) + (rand.Next() % (randomRange * 2)) + randomRange;
                    int y = (row * yPerRow) + (rand.Next() % (randomRange * 2)) + randomRange;
                    stars.Add(new Star() { 
                        x = x,
                        y = y
                    });
                }
            }


            //var image = new MagickImage()
            return null;
        }


    }
}
