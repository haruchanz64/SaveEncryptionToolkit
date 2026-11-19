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

    }
}