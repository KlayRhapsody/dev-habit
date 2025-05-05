
using System.Security.Cryptography;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;

namespace DevHabit.UnitTests.Services;

public sealed class EncryptionServiceTests
{
    private readonly EncryptionService _encryptionService;

    public EncryptionServiceTests()
    {
        IOptions<EncryptionOptions> options = Options.Create(new EncryptionOptions
        {
            Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });

        _encryptionService = new EncryptionService(options);
    }

    [Fact]
    public void Encrypt_ShouldReturnDifferentCipherText_WhenEncryptingSamePlainText()
    {
        // Arrange
        string plainText = "Hello, World!";

        // Act
        string cipherText1 = _encryptionService.Encrypt(plainText);
        string cipherText2 = _encryptionService.Encrypt(plainText);

        // Assert
        Assert.NotEqual(cipherText1, cipherText2);
    }

    [Fact]
    public void Decrypt_ShouldReturnPlainText_WhenDecryptingCorrectCipherText()
    {
        // Arrange
        string plainText = "Hello, World!";
        string cipherText = _encryptionService.Encrypt(plainText);

        // Act
        string decryptedText = _encryptionService.Decrypt(cipherText);

        // Assert
        Assert.Equal(plainText, decryptedText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64")]
    [InlineData("aW52YWxpZC1jaXBoZXJ0ZXh0")] // too short, missing IV
    public void Decrypt_ShouldThrowInvalidOperationException_WhenCipherTextIsInvalid(string invalidCipherText)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Decrypt(invalidCipherText));
    }

    [Fact]
    public void Decrypt_ShouldThrowInvalidOperationException_WhenChiperTextIsCorrupted()
    {
        // Arrange
        string plainText = "Hello, World!";
        string cipherText = _encryptionService.Encrypt(plainText);
        string corruptedCiphertext = cipherText[..^10] + new string('0', 10);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Decrypt(corruptedCiphertext));
    }

    [Fact]
    public void Encrypt_ShouldHandleLongPlainText()
    {
        // Arrange
        string longPlainText = new string('A', 10000); // 10,000 characters

        // Act
        string cipherText = _encryptionService.Encrypt(longPlainText);
        string decryptedText = _encryptionService.Decrypt(cipherText);

        // Assert
        Assert.Equal(longPlainText, decryptedText);
    }

    [Fact]
    public void Encrypt_ShouldHandleSpecialCharacters()
    {
        // Arrange
        string specialCharsPlainText = "!@#$%^&*()_+-=[]{}|;:'\",.<>?/~`";

        // Act
        string cipherText = _encryptionService.Encrypt(specialCharsPlainText);
        string decryptedText = _encryptionService.Decrypt(cipherText);

        // Assert
        Assert.Equal(specialCharsPlainText, decryptedText);
    }
}

