using System;
using System.Security.Cryptography;
using System.Text;

namespace SaveEncryptionToolkit.Encryption
{
    public static class KeyObfuscator
    {
        public static string Encode(string value, int saltLength = 12)
        {
            var salt = GenerateSalt(saltLength);
            var combined = value + salt;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
            return $"{salt}::{encoded}";
        }

        public static string Decode(string obfuscated)
        {
            var parts = obfuscated.Split(new[] { "::" }, StringSplitOptions.None);

            if (parts.Length != 2)
                throw new FormatException("Invalid obfuscated key format. Expected format: 'salt::base64string'.");

            var salt = parts[0];
            var encoded = parts[1];

            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                throw new FormatException("Base64 decoding failed. Input may be corrupted.");
            }

            var decoded = Encoding.UTF8.GetString(decodedBytes);

            if (!decoded.EndsWith(salt))
                throw new InvalidOperationException("Salt mismatch or tampered data.");

            return decoded[..^salt.Length];
        }

        private static string GenerateSalt(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var sb = new StringBuilder(length);
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[sizeof(uint)];
                for (var i = 0; i < length; i++)
                {
                    rng.GetBytes(buffer);
                    var num = BitConverter.ToUInt32(buffer, 0);
                    sb.Append(chars[(int)(num % chars.Length)]);
                }
            }
            return sb.ToString();
        }
    }
}
