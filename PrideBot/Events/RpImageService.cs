using System;
using System.Collections.Generic;
using System.Linq;

using PrideBot.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.Data.SqlClient;
using ImageMagick;

namespace PrideBot.Events
{
    public class RpImageService
    {
        private readonly IConfigurationRoot config;

        const double Mult128 = 1.0;

        public RpImageService(IConfigurationRoot config)
        {
            this.config = config;
        }

        public async Task<MemoryFile> WriteTestTextAsync(string pfpUrl, string  phrase)
        {
            using var image = await GetPfpWithYellowTextAsync(pfpUrl, phrase);
            return await image.WriteToMemoryFileAsync("texty");
        }

        async Task<MagickImage> GetPfpWithYellowTextAsync(string pfpUrl, string phrase)
        {
            var image = new MagickImage(await WebHelper.DownloadWebFileDataAsync(pfpUrl));
            using var textImage = GetYellowTextImage(phrase);

            //textImage.BackgroundColor = MagickColors.Transparent;
            textImage.Rotate(-10);
            //textImage.Resize(new Percentage(85));
            image.Composite(textImage, Gravity.Northwest,
                (int)(75 * Mult128) - (textImage.Width / 2), (int)(-5 * Mult128), CompositeOperator.Over);

            return image;
        }

        MagickImage GetYellowTextImage(string phrase)
        {
            var textWidth = (int)(130 * Mult128);
            var textColor = new MagickColor(255, 254, 65, 255);
            var outlineColor = MagickColors.Black;
            var outlineWidth = 6.0 * Mult128;
            var fontSize = 19.0 * Mult128;
            var lineSpacing = -15.0 * Mult128;
            //var imageWidth = textWidth;
            var imageHeight = (int)(200 * Mult128);

            var readSettings = new MagickReadSettings()
            {
                BackgroundColor = MagickColors.Transparent,
                //AntiAlias = false,
                //TextKerning = 1,
                Font = "Assets/Fonts/GloriaHallelujah-Regular.ttf",
                Width = textWidth,
                //Height = textHeight,
                FillColor = textColor,
                StrokeColor = outlineColor,
                FontPointsize = fontSize,
                StrokeWidth = outlineWidth,
                TextGravity = Gravity.North,
                TextInterlineSpacing = lineSpacing,
                StrokeAntiAlias = true,
                Height = imageHeight
            };
            var image = new MagickImage("caption:" + phrase, readSettings);

            // redraw without stroke and composite
            readSettings.StrokeColor = MagickColors.Transparent;
            //readSettings.Height = image.Height;
            using var redrawImage = new MagickImage("caption:" + phrase, readSettings);
            //redrawImage.Extent(image.Width, image.Height, Gravity.Center, MagickColors.Transparent);
            image.Composite(redrawImage, CompositeOperator.Over);
            return image;
        }
    }
}
