using ImageMagick;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Registration;
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
        const int FrameCount = 30;

        readonly IConfigurationRoot config;
        readonly ShipImageGenerator shipImageGenerator;

        public LeaderboardImageGenerator(IConfigurationRoot config, ShipImageGenerator shipImageGenerator)
        {
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
        }

        public async Task<string> WriteLeaderboardAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var path =
                await (await GenerateLeaderboardAsync(topShips, topRareShips))
                .WriteToWebFileAsync(config, "leaderboard");
            return path;
        }
        
        public async Task<MagickImageCollection> GenerateLeaderboardAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var rand = new Random();
            var bg = await GenerateBackgroundGifAsync(rand);
            var yurikoFiles = Directory.GetFiles("Assets/Leaderboard/Yurikos/").ToArray();
            var chosenYurikoFile = yurikoFiles[rand.Next(yurikoFiles.Length)];
            using var yurikoImage = new MagickImageCollection(await File.ReadAllBytesAsync(chosenYurikoFile));
            using var overlayImage = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlay.png"));

            var yurikoHeight = (double)bg.First().Height * 3.75 / 5.0;
            var sineAmplitude = 30;
            var sineMult = 1.0;

            for (int i = 0; i < bg.Count; i++)
            {
                var bgFrame = bg[i];
                var yurikoFrame = yurikoImage[i % yurikoImage.Count];
                var p = 100.0 * yurikoHeight / (double)yurikoFrame.Height;
                var sineHeight = -Math.Sin(((double)i * 2.0 * Math.PI) / (double)FrameCount) * sineAmplitude * sineMult;
                sineHeight = Math.Clamp(sineHeight, -sineAmplitude, sineAmplitude);
                yurikoFrame.Resize(new Percentage(p));
                bgFrame.Composite(yurikoFrame, Gravity.Northwest,
                    (bgFrame.Width - yurikoFrame.Width) / 2,
                    ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)sineHeight,
                    CompositeOperator.Over);

                // Add ship images
                for (int j = 0; j < 5; j++)
                {
                    var x = 60;
                    var y = 100 + (j * 76);
                    if (j >= topShips.Count)
                        break;
                    using var shipImage = await shipImageGenerator.GenerateShipImageAsync(topShips[j]);
                    shipImage.InterpolativeResize(shipImage.Width * 2, shipImage.Height * 2, PixelInterpolateMethod.Nearest);
                    bgFrame.Composite(shipImage, Gravity.Northwest, x, y, CompositeOperator.Over);
                }

                // Add rare ship images
                for (int j = 0; j < 5; j++)
                {
                    var x = 560;
                    var y = 100 + (j * 76);
                    if (j >= topRareShips.Count)
                        break;
                    using var shipImage = await shipImageGenerator.GenerateShipImageAsync(topRareShips[j]);
                    shipImage.InterpolativeResize(shipImage.Width * 2, shipImage.Height * 2, PixelInterpolateMethod.Nearest);
                    bgFrame.Composite(shipImage, Gravity.Northwest, x, y, CompositeOperator.Over);
                }

                bgFrame.Composite(overlayImage, Gravity.Northwest, 0, 0, CompositeOperator.Over);
            }

            return bg;
        }

        class Star
        {
            public int x;
            public int y;
            public bool sparkles;
            public int sparkleOffset;
        }

        public async Task<MagickImageCollection> CreateYurikoGifAsync()
        {
            using var frontImage = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Top.png"));
            var collection = new MagickImageCollection();
            for (int i = 0; i < FrameCount; i++)
            {
                var image = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Bottom.png"));
                if (i != 0)
                {
                    var pp = image.GetPixels().Cast<IPixel<byte>>()
                        .Select(a => new Pixel(a.X, a.Y, ShiftHue(a.ToColor(), -(double)i / (double)FrameCount).ToByteArray()));
                    image.GetPixels().SetPixel(pp);
                }
                image.GifDisposeMethod = GifDisposeMethod.Background;
                image.Composite(frontImage, Gravity.Northwest, 0, 0, CompositeOperator.Over);
                collection.Add(image);
            }
            return collection;
        }

        IMagickColor<byte> ShiftHue(IMagickColor<byte> color, double amount)
        {
            var hColor = ColorHSV.FromMagickColor(color);
            hColor.Hue = MathHelper.TrueMod(hColor.Hue + amount, 1.0);
            return hColor.ToMagickColor();
        }

        public async Task<MagickImageCollection> GenerateBackgroundGifAsync(Random rand)
        {
            var width = 1500/4;
            var height = 1000/4;
            var bgColor = new MagickColor("#0D0F27");
            var starRows = 6;
            var starColumns = 8;
            var xPerColumn = (width / starColumns) + (starColumns / 2);
            var yPerRow = (height / starRows) + (starRows / 2);
            var randomRange = 15;
            var skipChance = .3;
            var sparkleChance = .5;

            var stars = new List<Star>();
            for (int column = 0; column < starColumns; column++)
            {
                for (int row = 0; row < starRows; row++)
                {
                    if (rand.NextDouble() < skipChance)
                        continue;
                    int x = (column * xPerColumn) + (rand.Next() % (randomRange * 2)) + randomRange;
                    int y = (row * yPerRow) + (rand.Next() % (randomRange * 2)) + randomRange;
                    stars.Add(new Star()
                    { 
                        x = x,
                        y = y,
                        sparkles = rand.NextDouble() < sparkleChance,
                        sparkleOffset = rand.Next(FrameCount)
                    });
                }
            }


            using var starCollection= new MagickImageCollection(await File.ReadAllBytesAsync($"Assets/Leaderboard/Star.gif"));
            foreach (var starImage in starCollection)
            {
                starImage.InterpolativeResize(starImage.Width * 2, starImage.Height * 2, PixelInterpolateMethod.Nearest);
            }
            var collection = new MagickImageCollection();
            var isFirst = true;
            for (int i = 0; i < FrameCount; i++)
            {
                //var image = new MagickImage(isFirst ? new MagickColor("#050622") : MagickColors.Transparent, width, height);
                var image = new MagickImage(new MagickColor("#050622"), width, height);
                //image.GifDisposeMethod = isFirst ? GifDisposeMethod.None : GifDisposeMethod.Previous;
                for (int j = 0; j < stars.Count; j++)
                {
                    var star = stars[j];

                    var frame = starCollection.Count - 1;
                    if(star.sparkles)
                    {
                        var starFrame = MathHelper.TrueMod(i - star.sparkleOffset, FrameCount);
                        if (starFrame < starCollection.Count)
                            frame = starFrame;
                    }
                    image.Composite(starCollection[frame], Gravity.Northwest, star.x, star.y, CompositeOperator.Over);
                }
                //image.InterpolativeResize((int)(1920.0 /1.5), (int)(1080.0/1.5), PixelInterpolateMethod.Nearest);
                image.InterpolativeResize(width * 2, height * 2, PixelInterpolateMethod.Nearest);
                collection.Add(image);
                isFirst = false;
            }


            return collection;
        }


    }
}
