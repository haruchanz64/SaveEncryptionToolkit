using System;
using System.Security.Cryptography;

namespace SaveEncryptionToolkit.Encryption
{
    public static class KeyGenerator
    {
        public static (string obfuscatedKey, string obfuscatedIV) GenerateKeyAndIv()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            return (
                KeyObfuscator.Encode(Convert.ToBase64String(aes.Key)),
                KeyObfuscator.Encode(Convert.ToBase64String(aes.IV))
            );
        }
    }
}