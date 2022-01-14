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
        const int FrameCount = 60;
        const int SineFrameCount = 30;

        readonly IConfigurationRoot config;
        readonly ShipImageGenerator shipImageGenerator;

        public LeaderboardImageGenerator(IConfigurationRoot config, ShipImageGenerator shipImageGenerator)
        {
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
        }

        public async Task<string> WriteLeaderboardImageAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var image = await GenerateLeaderboardAsync(topShips, topRareShips);
            image.Write("Leady.gif");
            var path = await image.WriteToWebFileAsync(config, "leaderboard", overrideName: DateTime.Now.ToString("MMddHHmmss"));
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

        double GetSineT(double frame, double sineMult, bool clamp)
        {
            var t = -Math.Sin((frame * 2.0 * Math.PI) / (double)SineFrameCount) * sineMult;
            if (clamp)
                return t;
            else
                return Math.Clamp(t, -1.0, 1.0);
        }

        public async Task<MagickImageCollection> GenerateLeaderboardAsync(List<Ship> topShips, List<Ship> topRareShips)
        {
            var rand = new Random();
            var bgCollection = await GenerateBackgroundGifAsync(rand);
            //var yurikoFiles = Directory.GetFiles("Assets/Leaderboard/Yurikos/").ToArray();
            using var yurikoImage = new MagickImageCollection(await File.ReadAllBytesAsync("Assets/Leaderboard/Yurikos/puyumi.png"));
            using var yurikoGlitchImage = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Yurikos/glitch.png"));
            using var textOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/Text.png"));
            using var bottomRightOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/BottomRight.png"));
            using var bottomLeftOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/BottomLeft.png"));
            using var topLeftOverlay = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Overlays/TopLeft.png"));
            using var staticImage = new MagickImageCollection(await File.ReadAllBytesAsync("Assets/Leaderboard/Static.gif"));
            using var staticImage2 = new MagickImageCollection(await File.ReadAllBytesAsync("Assets/Leaderboard/Static2.gif"));
            using var yurikoOutlineImage = new MagickImage(await File.ReadAllBytesAsync("Assets/Leaderboard/Yurikos/outline.png"));

            var yurikoHeight = (double)bgCollection.First().Height * 4 / 5.0;
            var sineAmplitude = 30;
            var sineMult = 1.0;

            // resize yuriko
            var p = 100.0 * yurikoHeight / (double)yurikoImage.First().Height;
            foreach (var yFrame in yurikoImage)
            {
                yFrame.Resize(new Percentage(p));
            }
            yurikoGlitchImage.Resize(new Percentage(p));

            foreach (var staticFrame in staticImage)
            {
                var sizeGeo = new MagickGeometry(bgCollection.First().Width, bgCollection.First().Height);
                sizeGeo.IgnoreAspectRatio = true;
                staticFrame.Resize(sizeGeo);
                //staticFrame.Flip();
            }
            foreach (var staticFrame in staticImage2)
            {
                var sizeGeo = new MagickGeometry(bgCollection.First().Width, bgCollection.First().Height);
                sizeGeo.IgnoreAspectRatio = true;
                staticFrame.Resize(sizeGeo);
            }

            for (int i = 0; i < bgCollection.Count; i++)
            {
                var bgFrame = bgCollection[i];

                var sineT = GetSineT((double)i, sineMult, true);

                // Composite string images (with timeoffsets)
                CompositeSine(bgFrame, bottomRightOverlay, 8, -4, 30, -7,
                    GetSineT((double)(i - 2), sineMult * 1.25, false));
                CompositeSine(bgFrame, topLeftOverlay, -10, 3, -20, 4,
                    GetSineT((double)(i - 0), sineMult * 1.1, false));
                CompositeSine(bgFrame, bottomLeftOverlay, -90, -3, 20, 4,
                    GetSineT((double)(i + 1), sineMult * .8, false));

                //// Composite yuriko
                var yurikoFrame = yurikoImage[i % yurikoImage.Count];
                //bgFrame.Composite(yurikoFrame, Gravity.Northwest,
                //    (bgFrame.Width - yurikoFrame.Width) / 2,
                //    ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
                //    CompositeOperator.Over);

                // Composite text
                //if (i % staticImage.Count >= 7)
                var glitchFrames = new List<int>() { 4, 5, 6, 7, 8, 9, 10, 11 };
                if (glitchFrames.Contains(i % staticImage.Count))
                {
                    // Glitch bars
                    var barHeight = 5 + rand.Next(15);
                    var barDistance = 5 + rand.Next(10) + barHeight;
                    var yOffset = 5 + rand.Next(10);
                    using var barImage = new MagickImage(MagickColors.Transparent, bgFrame.Width, bgFrame.Height);
                    var y = yOffset;
                    var xOffset1 = -1 - rand.Next(4);
                    var xOffset2 = 2 + rand.Next(6);
                    while (y < bgFrame.Height)
                    {
                        barImage.Draw(new DrawableRectangle(0, y, bgFrame.Width, y + barHeight));
                        y += barDistance;
                    }
                    using var text1 = new MagickImage(textOverlay);
                    using var text2 = new MagickImage(textOverlay);
                    using var yuriko1 = new MagickImage(yurikoFrame);
                    using var yuriko2 = new MagickImage(yurikoFrame);
                    // Mask the bars
                    text1.Composite(barImage, CompositeOperator.DstIn);
                    text2.Composite(barImage, CompositeOperator.DstOut);
                    var skipFrames = new List<int>() { 7, 8, 9 };
                    if (skipFrames.Contains(i % staticImage.Count))
                    {
                        xOffset1 = 0;
                        xOffset2 = 0;

                        bgFrame.Composite(yurikoFrame, Gravity.Northwest,
                            (bgFrame.Width - yurikoFrame.Width) / 2,
                            ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
                            CompositeOperator.Over);
                    }
                    else
                    {
                        bgFrame.Composite(yurikoGlitchImage, Gravity.Northwest,
                            (bgFrame.Width - yurikoFrame.Width) / 2,
                            ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
                            CompositeOperator.Over);
                    }

                    //bgFrame.Composite(yuriko1, Gravity.Northwest,
                    //    (bgFrame.Width - yuriko1.Width) / 2 + xOffset1,
                    //    ((bgFrame.Height - yuriko1.Height) / 2) + (int)(sineT * sineAmplitude),
                    //    CompositeOperator.Over);

                    //bgFrame.Composite(yuriko2, Gravity.Northwest,
                    //    (bgFrame.Width - yuriko2.Width) / 2 + xOffset2,
                    //    ((bgFrame.Height - yuriko2.Height) / 2) + (int)(sineT * sineAmplitude),
                    //    CompositeOperator.Over);



                    bgFrame.Composite(text1, Gravity.Northwest, xOffset1, 0, CompositeOperator.Over);
                    bgFrame.Composite(text2, Gravity.Northwest, xOffset2, 0, CompositeOperator.Over);

                    // Composite static additive
                    var staticFrame = staticImage[i % staticImage.Count];
                    bgFrame.Composite(staticFrame, Gravity.Northwest, 0, 0, CompositeOperator.Plus);
                }
                else
                {
                    bgFrame.Composite(yurikoFrame, Gravity.Northwest,
                        (bgFrame.Width - yurikoFrame.Width) / 2,
                        ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
                        CompositeOperator.Over);

                    bgFrame.Composite(textOverlay, Gravity.Northwest, 0, 0, CompositeOperator.Over);

                    // Composite static 2 additive
                    //var staticFrame = staticImage2[i % staticImage2.Count];
                    var staticFrame = staticImage2[i % staticImage2.Count];
                    bgFrame.Composite(staticFrame, Gravity.Northwest, 0, 0, CompositeOperator.Plus);
                }


                //using var grayMask = new MagickImage(bgFrame);
                //grayMask.Grayscale();
                //grayMask.Composite(yurikoOutlineImage, Gravity.Northwest,
                //            (bgFrame.Width - yurikoFrame.Width) / 2,
                //            ((bgFrame.Height - yurikoFrame.Height) / 2) + (int)(sineT * sineAmplitude),
                //            CompositeOperator.DstIn);
                //bgFrame.Composite(grayMask, Gravity.Northwest, 0, 0, CompositeOperator.Over);



                //// Add ship images
                //for (int j = 0; j < 5; j++)
                //{
                //    var x = 60;
                //    var y = 100 + (j * 76);
                //    if (j >= topShips.Count)
                //        break;
                //    using var shipImage = await shipImageGenerator.GenerateShipImageAsync(topShips[j]);
                //    shipImage.InterpolativeResize(shipImage.Width * 2, shipImage.Height * 2, PixelInterpolateMethod.Nearest);
                //    bgFrame.Composite(shipImage, Gravity.Northwest, x, y, CompositeOperator.Over);
                //}

                //// Add rare ship images
                //for (int j = 0; j < 5; j++)
                //{
                //    var x = 560;
                //    var y = 100 + (j * 76);
                //    if (j >= topRareShips.Count)
                //        break;
                //    using var shipImage = await shipImageGenerator.GenerateShipImageAsync(topRareShips[j]);
                //    shipImage.InterpolativeResize(shipImage.Width * 2, shipImage.Height * 2, PixelInterpolateMethod.Nearest);
                //    bgFrame.Composite(shipImage, Gravity.Northwest, x, y, CompositeOperator.Over);
                //}

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

        public async Task<MagickImageCollection> CreateYurikoGifAsync(string frontImageUrl, string backGifUrl)
        {
            using var frontImage = new MagickImage(await WebHelper.DownloadWebFileDataAsync(frontImageUrl));
            var collection = new MagickImageCollection(await WebHelper.DownloadWebFileDataAsync(backGifUrl));
            collection.Coalesce();
            foreach (var image in collection)
            {
                image.Composite(frontImage, Gravity.Northwest, 0, 0, CompositeOperator.Over);
            }
            return collection;
        }

        //public async Task<MagickImageCollection> CreateYurikoGifAsync(string frontImageUrl, string backImageUrl)
        //{
        //    using var frontImage = new MagickImage(await WebHelper.DownloadWebFileDataAsync(frontImageUrl));
        //    var collection = new MagickImageCollection();
        //    for (int i = 0; i < 2; i++)
        //    {
        //        var image = new MagickImage(await WebHelper.DownloadWebFileDataAsync(frontImageUrl));
        //        if (i != 0)
        //        {
        //            var pp = image.GetPixels().Cast<IPixel<byte>>()
        //                .Select(a => new Pixel(a.X, a.Y, ShiftHue(a.ToColor(), -(double)i / (double)FrameCount).ToByteArray()));
        //            image.GetPixels().SetPixel(pp);
        //        }
        //        image.GifDisposeMethod = GifDisposeMethod.Background;
        //        image.Composite(frontImage, Gravity.Northwest, 0, 0, CompositeOperator.Over);
        //        collection.Add(image);
        //    }
        //    return collection;
        //}

        IMagickColor<byte> ShiftHue(IMagickColor<byte> color, double amount)
        {
            var hColor = ColorHSV.FromMagickColor(color);
            hColor.Hue = MathHelper.TrueMod(hColor.Hue + amount, 1.0);
            return hColor.ToMagickColor();
        }

        public async Task<MagickImageCollection> GenerateBackgroundGifAsync(Random rand)
        {
            var width = 1500 / 4;
            var height = 1000 / 4;
            var bgColor = new MagickColor("#0D0F27");
            var starRows = 9;
            var starColumns = 12;
            var xPerColumn = (width * 2 / starColumns) + (starColumns / 2);
            var yPerRow = (height * 2 / starRows) + (starRows / 2);
            var randomRange = 15;
            var skipChance = .3;
            var sparkleChance = 1.0;

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


            using var starCollection = new MagickImageCollection(await File.ReadAllBytesAsync($"Assets/Leaderboard/Star.gif"));
            foreach (var starImage in starCollection)
            {
                starImage.InterpolativeResize(starImage.Width * 2, starImage.Height * 2, PixelInterpolateMethod.Nearest);
            }
            var collection = new MagickImageCollection();
            var isFirst = true;
            for (int i = 0; i < FrameCount; i++)
            {
                //var image = new MagickImage(isFirst ? new MagickColor("#050622") : MagickColors.Transparent, width, height);
                //var image = new MagickImage(new MagickColor("#050622"), width, height);
                // Prototype
                //var image = new MagickImage(await WebHelper.DownloadWebFileDataAsync("https://cdn.discordapp.com/attachments/360819802350026764/886742398930137108/unknown.png"));
                // New test
                //var image = new MagickImage(await WebHelper.DownloadWebFileDataAsync("https://cdn.discordapp.com/attachments/885104659700805662/892130758007857203/unknown.png"));
                // New
                var image = new MagickImage(await WebHelper.DownloadWebFileDataAsync("https://cdn.discordapp.com/attachments/885104659700805662/892801720671948812/yurikospace.png"));
                //image.GifDisposeMethod = isFirst ? GifDisposeMethod.None : GifDisposeMethod.Previous;
                for (int j = 0; j < stars.Count; j++)
                {
                    var star = stars[j];

                    var frame = starCollection.Count - 1;
                    if (star.sparkles)
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
