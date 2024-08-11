using SixLabors.Fonts;

namespace OV_DB.Services;

public interface IFontLoader
{
    FontCollection FontCollection { get; }
}

public class FontLoader : IFontLoader
{
    public FontCollection FontCollection
    {
        get
        {
            if (_fontCollection == null)
            {
                _fontCollection = CreateFontCollection();
            }
            return _fontCollection;
        }
    }

    private FontCollection? _fontCollection = null;

    private FontCollection CreateFontCollection()
    {
        var fonts = new FontCollection();
        fonts.Add("Assets/Fonts/Ubuntu-Regular.ttf");
        return fonts;
    }
}
