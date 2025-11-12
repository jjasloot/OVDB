using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;

namespace OV_DB.Services
{
    public class AchievementIconService : IAchievementIconService
    {
        private readonly ILogger<AchievementIconService> _logger;
        private readonly string _iconBasePath;

        public AchievementIconService(ILogger<AchievementIconService> logger)
        {
            _logger = logger;
            _iconBasePath = Path.Combine("wwwroot", "achievements");
            
            // Ensure directory exists
            if (!Directory.Exists(_iconBasePath))
            {
                Directory.CreateDirectory(_iconBasePath);
            }
        }

        public async Task<string> GenerateAchievementIconAsync(string achievementKey, string level)
        {
            try
            {
                var fileName = $"{achievementKey}.png";
                var filePath = Path.Combine(_iconBasePath, fileName);

                // If icon already exists, return the URL
                if (File.Exists(filePath))
                {
                    return GetIconUrl(achievementKey, level);
                }

                // Generate a simple icon with the level color
                using (var image = new Image<Rgba32>(256, 256))
                {
                    var color = Color.ParseHex(GetLevelColor(level));
                    
                    // Fill background and draw a filled circle
                    image.Mutate(ctx => ctx
                        .Fill(Color.White)
                        .FillPolygon(color, CreateCirclePoints(128, 128, 100)));

                    // Save the image
                    await image.SaveAsPngAsync(filePath);
                }

                return GetIconUrl(achievementKey, level);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating achievement icon for {AchievementKey}", achievementKey);
                return null;
            }
        }

        public string GetIconUrl(string achievementKey, string level)
        {
            return $"/achievements/{achievementKey}.png";
        }

        private string GetLevelColor(string level)
        {
            return level?.ToLower() switch
            {
                "bronze" => "#CD7F32",
                "silver" => "#C0C0C0",
                "gold" => "#FFD700",
                "platinum" => "#E5E4E2",
                "diamond" => "#B9F2FF",
                _ => "#888888"
            };
        }

        private PointF[] CreateCirclePoints(float centerX, float centerY, float radius)
        {
            var points = new PointF[360];
            for (int i = 0; i < 360; i++)
            {
                var angle = i * Math.PI / 180.0;
                points[i] = new PointF(
                    centerX + (float)(radius * Math.Cos(angle)),
                    centerY + (float)(radius * Math.Sin(angle))
                );
            }
            return points;
        }
    }
}
