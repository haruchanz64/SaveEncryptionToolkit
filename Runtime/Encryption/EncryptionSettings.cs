using UnityEngine;

namespace SaveEncryptionToolkit.Encryption
{
    [CreateAssetMenu(fileName = "Encryption Settings", menuName = "Save Encryption Toolkit/Encryption Settings", order = 0)]
    public class EncryptionSettings : ScriptableObject
    {
        [Header("Base64 Encoded Key and IV")]
        [TextArea] public string encryptedKey;
        [TextArea] public string encryptedIv;

        public string GetEncryptedKey()
        {
            return KeyObfuscator.Decode(encryptedKey);
        }

        public string GetEncryptedIvKey()
        {
            return KeyObfuscator.Decode(encryptedIv);
        }

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