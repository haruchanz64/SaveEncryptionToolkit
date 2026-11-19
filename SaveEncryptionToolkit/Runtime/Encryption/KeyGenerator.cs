using System;
using System.Security.Cryptography;

namespace SaveEncryptionToolkit.Encryption
{
    public static class KeyGenerator
    {
        public static (string key, string iv) GenerateKeyAndIv()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            return (
                Convert.ToBase64String(aes.Key),
                Convert.ToBase64String(aes.IV)
            );
        }
    }
}