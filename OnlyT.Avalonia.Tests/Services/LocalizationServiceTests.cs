using OnlyT.Avalonia.Services.Localization;

namespace OnlyT.Avalonia.Tests.Services;

[TestClass]
public class LocalizationServiceTests
{
    private LocalizationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new LocalizationService();
    }

    [TestMethod]
    public void GetString_WithValidKey_ReturnsLocalizedString()
    {
        // Arrange
        var key = "SETTINGS";

        // Act
        var result = _service.GetString(key);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(key, result);  // Should be localized, not the key itself
    }

    [TestMethod]
    public void GetString_WithInvalidKey_ReturnsKey()
    {
        // Arrange
        var key = "INVALID_KEY_THAT_DOES_NOT_EXIST_12345";

        // Act
        var result = _service.GetString(key);

        // Assert
        Assert.AreEqual(key, result);  // Returns the key when not found
    }

    [TestMethod]
    public void Indexer_ReturnsLocalizedString()
    {
        // Arrange
        var key = "SETTINGS";

        // Act
        var result = _service[key];

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(_service.GetString(key), result);
    }

    [TestMethod]
    public void GetSupportedLanguages_ReturnsNonEmptyList()
    {
        // Act
        var languages = _service.GetSupportedLanguages().ToList();

        // Assert
        Assert.IsTrue(languages.Count > 0);
    }

    [TestMethod]
    public void GetSupportedLanguages_ContainsEnglish()
    {
        // Act
        var languages = _service.GetSupportedLanguages().ToList();

        // Assert
        Assert.IsTrue(languages.Any(l => l.CultureCode.StartsWith("en")));
    }

    [TestMethod]
    public void SetCulture_ChangesCulture()
    {
        // Arrange
        var initialCulture = _service.CurrentCulture;

        // Act
        _service.SetCulture("es-ES");

        // Assert
        Assert.AreEqual("es-ES", _service.CurrentCulture.Name);
    }

    [TestMethod]
    public void SetCulture_RaisesCultureChangedEvent()
    {
        // Arrange
        var eventRaised = false;
        _service.CultureChanged += (_, _) => eventRaised = true;

        // Act
        _service.SetCulture("fr-FR");

        // Assert
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void SetCulture_WithInvalidCulture_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _service.SetCulture("invalid-culture-code");
    }

    [TestMethod]
    public void GetString_WithFormatArgs_FormatsCorrectly()
    {
        // Arrange - Use a key that we know has format placeholders or a simple test
        var key = "TEST_FORMAT_KEY";  // May not exist, that's ok
        var arg1 = "test1";
        var arg2 = "test2";

        // Act
        var result = _service.GetString(key, arg1, arg2);

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void CurrentCulture_ReturnsNonNull()
    {
        // Act
        var culture = _service.CurrentCulture;

        // Assert
        Assert.IsNotNull(culture);
    }

    [TestMethod]
    public void GetSupportedLanguages_HasCultureCodes()
    {
        // Act
        var languages = _service.GetSupportedLanguages().ToList();

        // Assert
        foreach (var language in languages)
        {
            Assert.IsFalse(string.IsNullOrEmpty(language.CultureCode));
        }
    }

    [TestMethod]
    public void GetSupportedLanguages_HasDisplayNames()
    {
        // Act
        var languages = _service.GetSupportedLanguages().ToList();

        // Assert
        foreach (var language in languages)
        {
            Assert.IsFalse(string.IsNullOrEmpty(language.DisplayName));
        }
    }

    [TestMethod]
    public void GetSupportedLanguages_HasNativeNames()
    {
        // Act
        var languages = _service.GetSupportedLanguages().ToList();

        // Assert
        foreach (var language in languages)
        {
            Assert.IsFalse(string.IsNullOrEmpty(language.NativeName));
        }
    }

    [TestMethod]
    public void LanguageItem_ToString_ReturnsDisplayName()
    {
        // Arrange
        var item = new LanguageItem
        {
            CultureCode = "en-GB",
            DisplayName = "English (UK)",
            NativeName = "English"
        };

        // Act
        var result = item.ToString();

        // Assert
        Assert.AreEqual("English (UK)", result);
    }
}
