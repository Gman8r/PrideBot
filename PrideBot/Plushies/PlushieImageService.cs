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

namespace PrideBot.Plushies
{
    public class PlushieImageService
    {
        private readonly IConfigurationRoot config;

        public PlushieImageService(IConfigurationRoot config)
        {
            this.config = config;
        }

        public async Task<MemoryFile> WritePlushieImageAsync(UserPlushie userPlushie)
        {
            using var charImage = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/{userPlushie.CharacterId}.png"));
            charImage.BackgroundColor = MagickColors.Transparent;
            charImage.Modulate(new Percentage(115), new Percentage(90), new Percentage(100));
            charImage.Rotate((double)userPlushie.Rotation);
            charImage.Extent(48, 48, Gravity.Center, MagickColors.Transparent);
            charImage.InterpolativeResize(charImage.Width * 16, charImage.Height * 16, PixelInterpolateMethod.Nearest);

            return await charImage.WriteToMemoryFileAsync("plushie");
        }
    }
}
