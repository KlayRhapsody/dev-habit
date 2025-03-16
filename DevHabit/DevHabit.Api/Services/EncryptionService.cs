using System.Security.Cryptography;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Services;

public sealed class EncryptionService(IOptions<EncryptionOptions> options)
{
    private readonly byte[] _masterKey = Convert.FromBase64String(options.Value.Key);
    private const int IVSize = 16;
    public string Encrypt(string plaintText)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _masterKey;
            aes.IV = RandomNumberGenerator.GetBytes(IVSize);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(aes.IV, 0, IVSize);

            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            using (var streamWriter = new StreamWriter(cryptoStream))
            {
                streamWriter.Write(plaintText);
            }

            return Convert.ToBase64String(memoryStream.ToArray());
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to encrypt the text", ex);
        }
    }

    public string Decrypt(string chipertText)
    {
        try
        {
            byte[] chiperData = Convert.FromBase64String(chipertText);

            if (chiperData.Length < IVSize)
            {
                throw new InvalidOperationException("Chiper text is too short");
            }

            byte[] iv = new byte[IVSize];
            byte[] encryptedData = new byte[chiperData.Length - IVSize];

            Buffer.BlockCopy(chiperData, 0, iv, 0, IVSize);
            Buffer.BlockCopy(chiperData, IVSize, encryptedData, 0, encryptedData.Length);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _masterKey;
            aes.IV = iv;

            using var mermoryStream = new MemoryStream(encryptedData);
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(mermoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);

            return streamReader.ReadToEnd();
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to decrypt the text", ex);
        }
    }
}
