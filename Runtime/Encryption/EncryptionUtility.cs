using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveEncryptionToolkit.Encryption
{
    public static class EncryptionUtility
    {
        private static byte[] _key;
        private static byte[] _iv;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _key = null;
            _iv = null;
        }

        public static void Initialize(EncryptionSettings encryptionSettings)
        {
            if (encryptionSettings == null)
                throw new ArgumentNullException(nameof(encryptionSettings));

            Initialize(encryptionSettings.GetEncryptedKey(), encryptionSettings.GetEncryptedIvKey());
        }

        public static void Initialize(string base64Key, string base64Iv)
        {
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentException("AES key cannot be null or empty.", nameof(base64Key));

            if (string.IsNullOrEmpty(base64Iv))
                throw new ArgumentException("AES IV cannot be null or empty.", nameof(base64Iv));

            var key = Convert.FromBase64String(base64Key);
            var iv = Convert.FromBase64String(base64Iv);

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 128, 192, or 256 bits (16, 24, or 32 bytes).", nameof(base64Key));

            if (iv.Length != 16)
                throw new ArgumentException("AES IV must be 128 bits (16 bytes).", nameof(base64Iv));

            _key = key;
            _iv = iv;
        }

        public static string Encrypt(string plainText)
        {
            if (_key == null || _iv == null)
                throw new InvalidOperationException("EncryptionUtility is not initialized. Call Initialize() first.");

            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var encryptor = aes.CreateEncryptor();
            var buffer = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(buffer, 0, buffer.Length);

            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string cipherText)
        {
            if (_key == null || _iv == null)
                throw new InvalidOperationException("EncryptionUtility is not initialized. Call Initialize() first.");

            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentException("Cipher text cannot be null or empty.", nameof(cipherText));

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor();
            var buffer = Convert.FromBase64String(cipherText);
            var decrypted = decryptor.TransformFinalBlock(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}