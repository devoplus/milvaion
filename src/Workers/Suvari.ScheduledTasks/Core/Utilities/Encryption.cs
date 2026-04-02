using System.Security.Cryptography;
using System.Text;

namespace Suvari.ScheduledTasks.Core.Utilities;

/// <summary>
/// Kriptolama işlemleri için kullanılan helperlar
/// </summary>
public class Encryption
{
    private static readonly byte[] _initVectorBytes = Encoding.ASCII.GetBytes("tu89geji340t89u2");
    private const int _keysize = 256;

    public static string Encrypt(string plainText, string passPhrase)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        using var password = new PasswordDeriveBytes(passPhrase, null);
        byte[] keyBytes = password.GetBytes(_keysize / 8);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;

        using var encryptor    = aes.CreateEncryptor(keyBytes, _initVectorBytes);
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
        cryptoStream.FlushFinalBlock();
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public static string Decrypt(string cipherText, string passPhrase)
    {
        byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
        using var password = new PasswordDeriveBytes(passPhrase, null);
        byte[] keyBytes = password.GetBytes(_keysize / 8);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;

        using var decryptor    = aes.CreateDecryptor(keyBytes, _initVectorBytes);
        using var memoryStream = new MemoryStream(cipherTextBytes);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

        byte[] plainTextBytes     = new byte[cipherTextBytes.Length];
        int    decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
        return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
    }

    private const string _encDecKey = "99431111";

    [Obsolete("Bu metod yerine Encrypt metodu kullanılmalıdır.")]
    public static string EncryptText(string inputText)
    {
        using var aes = Aes.Create();
        byte[] plainText = Encoding.Unicode.GetBytes(inputText);
        byte[] salt = Encoding.ASCII.GetBytes(_encDecKey.Length.ToString());
        using var secretKey = new PasswordDeriveBytes(_encDecKey, salt);
        using var encryptor    = aes.CreateEncryptor(secretKey.GetBytes(32), secretKey.GetBytes(16));
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
        cryptoStream.Write(plainText, 0, plainText.Length);
        cryptoStream.FlushFinalBlock();
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    [Obsolete("Bu metod yerine Decrypt metodu kullanılmalıdır.")]
    public static string DecryptText(string inputText)
    {
        using var aes = Aes.Create();
        inputText = inputText.Replace(" ", "+");
        byte[] encryptedData = Convert.FromBase64String(inputText);
        byte[] salt = Encoding.ASCII.GetBytes(_encDecKey.Length.ToString());
        using var secretKey = new PasswordDeriveBytes(_encDecKey, salt);
        using var decryptor    = aes.CreateDecryptor(secretKey.GetBytes(32), secretKey.GetBytes(16));
        using var memoryStream = new MemoryStream(encryptedData);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        byte[] plainText = new byte[encryptedData.Length];
        int decryptedCount = cryptoStream.Read(plainText, 0, plainText.Length);
        return Encoding.Unicode.GetString(plainText, 0, decryptedCount);
    }

    /// <summary>
    /// SHA256 encryption için kullanılır.
    /// </summary>
    /// <param name="inputString">Encrypt edilecek string</param>
    /// <returns>Encrypt edilmiş değer</returns>
    public static string GenerateSHA256String(string inputString)
    {
        SHA256 sha256 = SHA256Managed.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(inputString);
        byte[] hash = sha256.ComputeHash(bytes);
        return GetStringFromHash(hash);
    }

    /// <summary>
    /// SHA512 encryption için kullanılır.
    /// </summary>
    /// <param name="inputString">Encrypt edilecek string</param>
    /// <returns>Encrypt edilmiş değer</returns>
    public static string GenerateSHA512String(string inputString)
    {
        SHA512 sha512 = SHA512Managed.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(inputString);
        byte[] hash = sha512.ComputeHash(bytes);
        return GetStringFromHash(hash);
    }

    /// <summary>
    /// Base64 ecnryption için kullanılır.
    /// </summary>
    /// <param name="plainText">Encrypt edilecek string</param>
    /// <returns>Encrypt edilmiş değer</returns>
    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    /// <summary>
    /// Base64 decrypt için kullanılır.
    /// </summary>
    /// <param name="base64EncodedData">Encrypt edilmiş değer</param>
    /// <returns>Decrypt edilmiş değer</returns>
    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    /// <summary>
    /// Encrypt edilmiş byte array'i string'e çevirir.
    /// </summary>
    /// <param name="hash">Byte array</param>
    /// <returns>String sonucu</returns>
    private static string GetStringFromHash(byte[] hash)
    {
        StringBuilder result = new StringBuilder();

        for (int i = 0; i < hash.Length; i++)
        {
            result.Append(hash[i].ToString("X2"));
        }
        return result.ToString();
    }
}