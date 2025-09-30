using OV_DB.Helpers;
using OVDB_database.Enums;

namespace OV_DB.Tests.Helpers
{
    public class LanguageHelperTests
    {
        [Fact]
        public void ToLanguageCode_WithEnglish_ReturnsEn()
        {
            // Arrange
            var language = PreferredLanguage.English;

            // Act
            var code = language.ToLanguageCode();

            // Assert
            Assert.Equal("en", code);
        }

        [Fact]
        public void ToLanguageCode_WithDutch_ReturnsNl()
        {
            // Arrange
            var language = PreferredLanguage.Dutch;

            // Act
            var code = language.ToLanguageCode();

            // Assert
            Assert.Equal("nl", code);
        }

        [Fact]
        public void FromLanguageCode_WithEn_ReturnsEnglish()
        {
            // Act
            var language = LanguageHelper.FromLanguageCode("en");

            // Assert
            Assert.Equal(PreferredLanguage.English, language);
        }

        [Fact]
        public void FromLanguageCode_WithNl_ReturnsDutch()
        {
            // Act
            var language = LanguageHelper.FromLanguageCode("nl");

            // Assert
            Assert.Equal(PreferredLanguage.Dutch, language);
        }

        [Fact]
        public void FromLanguageCode_WithNullOrInvalid_ReturnsEnglish()
        {
            // Act & Assert
            Assert.Equal(PreferredLanguage.English, LanguageHelper.FromLanguageCode(null));
            Assert.Equal(PreferredLanguage.English, LanguageHelper.FromLanguageCode(""));
            Assert.Equal(PreferredLanguage.English, LanguageHelper.FromLanguageCode("invalid"));
            Assert.Equal(PreferredLanguage.English, LanguageHelper.FromLanguageCode("fr"));
        }

        [Theory]
        [InlineData("en", PreferredLanguage.English)]
        [InlineData("EN", PreferredLanguage.English)]
        [InlineData("nl", PreferredLanguage.Dutch)]
        [InlineData("NL", PreferredLanguage.Dutch)]
        public void FromLanguageCode_IsCaseInsensitive(string code, PreferredLanguage expected)
        {
            // Act
            var language = LanguageHelper.FromLanguageCode(code);

            // Assert
            Assert.Equal(expected, language);
        }
    }
}
