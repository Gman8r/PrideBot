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

        public async Task<string> WriteLeaderboardImageAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var path =
                await (await GenerateLeaderboardAsync(topShips, topRareShips))
                .WriteToWebFileAsync(config, "leaderboard");
            return path;
        }


        public void CompositeSine(IMagickImage<byte> baseImage, IMagickImage<byte> overlayImage, int xOffset, int xAmplitude, int yOffset, int yAmplitude, double sineT)
        {
            var x = (int)(sineT * (double)xAmplitude);
            var y = (int)(sineT * (double)yAmplitude);
            baseImage.Composite(overlayImage, Gravity.Northwest, xOffset + x, yOffset + y, CompositeOperator.Over);
        }

        void WhitenImage(IMagickImage<byte> image)
        {
            var pp = image.GetPixels().Cast<IPixel<byte>>()
                .Select(a => new Pixel(a.X, a.Y, MakeWhite(a.ToColor()).ToByteArray()));
            image.GetPixels().SetPixel(pp);
        }

        IMagickColor<byte> MakeWhite(IMagickColor<byte> color)
        {
            color.R = color.G = color.B = MagickColors.White.R;
            return color;
        }

        public async Task<MagickImageCollection> GenerateLeaderboardAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var rand = new Random();
            var bgCollection = await GenerateBackgroundGifAsync(rand);
            var yurikoFiles = Directory.GetFiles("Assets/Leaderboard/Yurikos/").ToArray();
            var chosenYurikoFile = yurikoFiles[rand.Next(yurikoFiles.Length)];
            using var yurikoImage = new MagickImageCollection(await File.ReadAllBytesAsync(chosenYurikoFile));
            using var textOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/Text.png"));
            using var bottomRightOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/BottomRight.png"));
            using var bottomLeftOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/BottomLeft.png"));
            using var topLeftOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/TopLeft.png"));

            var yurikoHeight = (double)bgCollection.First().Height * 4 / 5.0;
            var sineAmplitude = 30;
            var sineMult = 1.0;

            for (int i = 0; i < bgCollection.Count; i++)
            {
                var bgFrame = bgCollection[i];
                var sineT = -Math.Sin(((double)i * 2.0 * Math.PI) / (double)FrameCount) * sineMult;
                sineT = Math.Clamp(sineT, -1.0, 1.0);

                // Composite string images
                CompositeSine(bgFrame, bottomRightOverlay, 8, -4, 30, -7, sineT);
                CompositeSine(bgFrame, topLeftOverlay, -10, 3, -20, 4, sineT);
                CompositeSine(bgFrame, bottomLeftOverlay, -90, -3, 20, 4, sineT);

                // Composite yuriko
                var yurikoFrame = yurikoImage[i % yurikoImage.Count];
                var p = 100.0 * yurikoHeight / (double)yurikoFrame.Height;
                yurikoFrame.Resize(new Percentage(p));
                bgFrame.Composite(yurikoFrame, Gravity.Northwest,
                    (bgFrame.Width - yurikoFrame.Width) / 2,
                    ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
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
                    //using var whiteImage = new MagickImage(shipImage);
                    //WhitenImage(whiteImage);
                    //bgFrame.Composite(whiteImage, Gravity.Northwest, x - 1, y, CompositeOperator.Over);
                    //bgFrame.Composite(whiteImage, Gravity.Northwest, x + 1, y, CompositeOperator.Over);
                    //bgFrame.Composite(whiteImage, Gravity.Northwest, x, y - 1, CompositeOperator.Over);
                    //bgFrame.Composite(whiteImage, Gravity.Northwest, x, y + 1, CompositeOperator.Over);
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

                bgFrame.Composite(textOverlay, Gravity.Northwest, 0, 0, CompositeOperator.Over);
            }

            return bgCollection;
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
                            image.Composite(starCollection[starFrame], Gravity.Northwest, star.x, star.y, CompositeOperator.Over);
                    }
                    else
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
