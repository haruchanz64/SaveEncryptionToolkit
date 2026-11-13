using System;
using System.Security.Cryptography;

namespace SaveEncryptionToolkit.Encryption
{
    /// <summary>
    /// Generates cryptographically random AES key and IV pairs via <see cref="Aes.Create()"/>.
    /// </summary>
    public static class KeyGenerator
    {
        /// <summary>
        /// Generates a fresh 256-bit AES key and 128-bit IV encoded as Base64 strings.
        /// </summary>
        /// <returns>A tuple containing the Base64-encoded AES <c>key</c> (32 raw bytes) and <c>iv</c> (16 raw bytes).</returns>
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