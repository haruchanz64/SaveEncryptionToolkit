using UnityEngine;

namespace SaveEncryptionToolkit.Encryption
{
    /// <summary>
    /// ScriptableObject asset that stores obfuscated AES key and IV values for editor setup and runtime initialization.
    /// </summary>
    [CreateAssetMenu(fileName = "Encryption Settings", menuName = "Save Encryption Toolkit/Encryption Settings", order = 0)]
    public class EncryptionSettings : ScriptableObject
    {
        /// <summary>
        /// Obfuscated AES key string in <c>salt::base64</c> format produced by <see cref="KeyObfuscator.Encode"/>.
        /// </summary>
        [Header("Base64 Encoded Key and IV")]
        [TextArea] public string encryptedKey;
        /// <summary>
        /// Obfuscated AES initialization vector string in <c>salt::base64</c> format produced by <see cref="KeyObfuscator.Encode"/>.
        /// </summary>
        [TextArea] public string encryptedIv;

        /// <summary>
        /// Decodes and returns the raw Base64-encoded AES key stored in <see cref="encryptedKey"/>.
        /// </summary>
        /// <returns>The decoded Base64 AES key string.</returns>
        public string GetEncryptedKey()
        {
            return KeyObfuscator.Decode(encryptedKey);
        }

        /// <summary>
        /// Decodes and returns the raw Base64-encoded AES IV stored in <see cref="encryptedIv"/>.
        /// </summary>
        /// <returns>The decoded Base64 AES IV string.</returns>
        public string GetEncryptedIvKey()
        {
            return KeyObfuscator.Decode(encryptedIv);
        }

        /// <summary>
        /// Checks whether <see cref="encryptedKey"/> and <see cref="encryptedIv"/> decode cleanly into a valid AES key (16, 24, or 32 bytes) and a 16-byte IV.
        /// </summary>
        /// <returns><c>true</c> if both values are properly obfuscated and decode to valid AES key and IV byte lengths; otherwise <c>false</c>.</returns>
        public bool HasValidKeys()
        {
            if (!KeyObfuscator.TryDecode(encryptedKey, out var rawKey) ||
                !KeyObfuscator.TryDecode(encryptedIv, out var rawIv))
            {
                return false;
            }

            try
            {
                var keyBytes = System.Convert.FromBase64String(rawKey);
                var ivBytes = System.Convert.FromBase64String(rawIv);
                var validKeyLength = keyBytes.Length == 16 || keyBytes.Length == 24 || keyBytes.Length == 32;
                return validKeyLength && ivBytes.Length == 16;
            }
            catch (System.FormatException)
            {
                return false;
            }
        }
    }
}