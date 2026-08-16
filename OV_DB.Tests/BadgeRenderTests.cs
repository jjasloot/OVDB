using System;
using System.Collections.Generic;
using System.IO;
using OV_DB.Controllers;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OV_DB.Tests
{
    // The badge renderer does a lot of manual pixel maths (columns, bars, overflow markers), and
    // it runs on an anonymous endpoint - so a layout mistake becomes a 500 for an embedded image.
    // These tests only assert that it always produces a valid PNG of the requested size.
    public class BadgeRenderTests
    {
        private static readonly Lazy<FontFamily> Font = new(() =>
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "OV_DB", "Assets", "Fonts", "Ubuntu-Regular.ttf");
                if (File.Exists(candidate))
                {
                    var collection = new FontCollection();
                    collection.Add(candidate);
                    return collection.Get("Ubuntu");
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Could not locate Ubuntu-Regular.ttf from the test output directory.");
        });

        private static List<ImagesController.TypeRow> SampleRows() =>
        [
            new("Trein", Color.ParseHex("1976D2"), 18452.4, 1204.5),
            new("Metro", Color.ParseHex("E64A19"), 3120.8, 245.0),
            new("Tram", Color.ParseHex("7B1FA2"), 1875.2, 98.4),
            new("Bus", Color.ParseHex("388E3C"), 942.6, 61.2),
            new("Veerboot", Color.ParseHex("0097A7"), 310.0, 0),
        ];

        private static Image<Rgba32> Render(IReadOnlyList<ImagesController.TypeRow> rows, int width, int height, string theme, bool includeTotal = true, string title = "OV Database")
        {
            var bytes = ImagesController.RenderBadge(
                rows, Font.Value, width, height, title, includeTotal, dutch: true,
                hideAttribution: false, ImagesController.GetPalette(theme), new DateTime(2026, 8, 16, 12, 30, 0));
            return Image.Load<Rgba32>(bytes);
        }

        [Theory]
        [InlineData("light")]
        [InlineData("dark")]
        [InlineData("transparent")]
        [InlineData("unknown-theme-falls-back-to-light")]
        public void RenderBadge_ProducesAnImageOfTheRequestedSize(string theme)
        {
            using var image = Render(SampleRows(), 420, 170, theme);

            Assert.Equal(420, image.Width);
            Assert.Equal(170, image.Height);
        }

        [Fact]
        public void RenderBadge_LeavesTheBackgroundTransparentForTheTransparentTheme()
        {
            using var image = Render(SampleRows(), 420, 170, "transparent");

            Assert.Equal(0, image[1, 1].A);
        }

        [Fact]
        public void RenderBadge_PaintsAnOpaqueBackgroundForTheLightTheme()
        {
            using var image = Render(SampleRows(), 420, 170, "light");

            Assert.Equal(255, image[1, 1].A);
        }

        [Fact]
        public void RenderBadge_HandlesNoTripsWithoutThrowing()
        {
            using var image = Render([], 420, 170, "light");

            Assert.Equal(420, image.Width);
        }

        [Theory]
        [InlineData(20, 20)]     // the smallest size the endpoint clamps to
        [InlineData(60, 24)]
        [InlineData(2000, 2000)] // the largest size the endpoint clamps to
        public void RenderBadge_SurvivesExtremeDimensions(int width, int height)
        {
            using var image = Render(SampleRows(), width, height, "light");

            Assert.Equal(width, image.Width);
            Assert.Equal(height, image.Height);
        }

        [Fact]
        public void RenderBadge_WorksWithoutATitleOrTotal()
        {
            using var image = Render(SampleRows(), 300, 100, "light", includeTotal: false, title: null);

            Assert.Equal(300, image.Width);
            Assert.Equal(100, image.Height);
        }
    }
}
