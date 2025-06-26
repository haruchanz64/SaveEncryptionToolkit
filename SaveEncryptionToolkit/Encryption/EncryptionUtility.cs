using System;
using System.Security.Cryptography;
using System.Text;

namespace SaveEncryptionToolkit.Encryption
{
    public static class EncryptionUtility
    {
        private static EncryptionSettings _encryptionSettings;

        public static void Initialize(EncryptionSettings encryptionSettings)
        {
            _encryptionSettings = encryptionSettings ?? throw new ArgumentNullException(nameof(encryptionSettings));
        }

        public static string Encrypt(string plainText)
        {
            if (!_encryptionSettings)
                throw new InvalidOperationException("EncryptionUtility is not initialized. Call Initialize() first.");

            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(_encryptionSettings.GetEncryptedKey());
            aes.IV = Convert.FromBase64String(_encryptionSettings.GetEncryptedIvKey());

            var encrypt = aes.CreateEncryptor();
            var buffer = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encrypt.TransformFinalBlock(buffer, 0, buffer.Length);

            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string cipherText)
        {
            if (!_encryptionSettings)
                throw new InvalidOperationException("EncryptionUtility is not initialized. Call Initialize() first.");

            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(_encryptionSettings.GetEncryptedKey());
            aes.IV = Convert.FromBase64String(_encryptionSettings.GetEncryptedIvKey());

            var decrypt = aes.CreateDecryptor();
            var buffer = Convert.FromBase64String(cipherText);
            var decrypted = decrypt.TransformFinalBlock(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}