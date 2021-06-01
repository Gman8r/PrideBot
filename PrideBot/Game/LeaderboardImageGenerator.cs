using ImageMagick;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Game
{
    public class LeaderboardImageGenerator
    {
        readonly IConfigurationRoot config;

        public LeaderboardImageGenerator(IConfigurationRoot config)
        {
            this.config = config;
        }

        public async Task<string> WriteLeaderboardAsync()
        {
            var path =
                await (await GenerateLeaderboardAsync())
                .WriteToWebFileAsync(config, "leaderboard");
            return path;
        }

        public async Task<MagickImageCollection> GenerateLeaderboardAsync()
        {
            return await CreateYurikoGifAsync();
        }

        class Star
        {
            public int x;
            public int y;
        }

        public async Task<MagickImageCollection> CreateYurikoGifAsync()
        {
            MagickImage image = null;
            var collection = new MagickImageCollection();
            var frameCount = 2;
            for (int i = 0; i < frameCount; i++)
            {
                if (image == null)
                    image = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Bottom.png"));
                else
                    image = new MagickImage(image);
                var pp = image.GetPixels().Cast<IPixel<byte>>()
                    .Select(a => new Pixel(a.X, a.Y, ShiftHue(a.ToColor(), (double)i / (double)frameCount).ToByteArray()));
                image.GetPixels().SetPixel(pp);
                image.GifDisposeMethod = GifDisposeMethod.Background;
                collection.Add(image);
            }
            return collection;
        }

        IMagickColor<byte> ShiftHue(IMagickColor<byte> color, double amount)
        {
            var hColor = ColorHSV.FromMagickColor(color);
            hColor.Hue += amount;
            return hColor.ToMagickColor();
        }

        public async Task<MagickImageCollection> GenerateBackgroundGifAsync()
        {
            var rand = new Random();
            var width = 1920/4;
            var height = 1080/4;
            var bgColor = new MagickColor("#0D0F27");
            var starRows = 10;
            var starColumns = 20;
            var xPerColumn = (width / starColumns) + (starColumns / 2);
            var yPerRow = (height / starRows) + (starRows / 2);
            var randomRange = 10;
            var skipChance = .3;

            var stars = new List<Star>();
            for (int column = 0; column < starColumns; column++)
            {
                for (int row = 0; row < starRows; row++)
                {
                    if (rand.NextDouble() < skipChance)
                        continue;
                    int x = (column * xPerColumn) + (rand.Next() % (randomRange * 2)) + randomRange;
                    int y = (row * yPerRow) + (rand.Next() % (randomRange * 2)) + randomRange;
                    stars.Add(new Star() { 
                        x = x,
                        y = y
                    });
                }
            }


            var image = new MagickImage(new MagickColor("#050622"), width, height);
            var starPath = $"Assets/Stars/Small.png";
            using var starImage = new MagickImage(await File.ReadAllBytesAsync(starPath));
            foreach (var star in stars)
            {
                image.Composite(starImage, Gravity.Northwest, star.x, star.y, CompositeOperator.Over);
            }
            var h = image.GetPixels().FirstOrDefault();
            //image.TransformColorSpace()

            //var pp = image.GetPixels().Cast<IPixel<byte>>()
            //    .Select(a => new Pixel(a.X, a.Y, ShiftHue(a.ToColor()).ToByteArray()));
            //image.GetPixels().SetPixel(pp);


            image.InterpolativeResize(1920, 1080, PixelInterpolateMethod.Nearest);
            return new MagickImageCollection(new List<MagickImage>() { image });
        }


    }
}
