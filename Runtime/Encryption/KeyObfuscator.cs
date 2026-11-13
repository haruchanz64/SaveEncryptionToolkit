using System;
using System.Security.Cryptography;
using System.Text;

namespace SaveEncryptionToolkit.Encryption
{
    /// <summary>
    /// Applies salted Base64 obfuscation (<c>salt::base64(value + salt)</c>) to avoid storing raw Base64 keys verbatim in assets or generated C# files.
    /// This is obfuscation with tamper detection rather than cryptographic key protection.
    /// </summary>
    public static class KeyObfuscator
    {
        /// <summary>
        /// Obfuscates a string by appending a random alphanumeric salt, Base64-encoding the UTF-8 bytes, and prefixing the salt with a <c>::</c> delimiter.
        /// </summary>
        /// <param name="value">Plain string to obfuscate. Must not be <c>null</c>.</param>
        /// <param name="saltLength">Length of the random alphanumeric salt in characters. Must be greater than zero.</param>
        /// <returns>The obfuscated string in <c>salt::base64</c> format.</returns>
        public static string Encode(string value, int saltLength = 12)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (saltLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(saltLength), "Salt length must be greater than zero.");

            var salt = GenerateSalt(saltLength);
            var combined = value + salt;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
            return $"{salt}::{encoded}";
        }

        /// <summary>
        /// Decodes an obfuscated string produced by <see cref="Encode"/> and verifies that the embedded trailing salt matches the prefix.
        /// </summary>
        /// <param name="obfuscated">String in <c>salt::base64</c> format.</param>
        /// <returns>The original unobfuscated string.</returns>
        public static string Decode(string obfuscated)
        {
            if (string.IsNullOrEmpty(obfuscated))
                throw new ArgumentException("Obfuscated key cannot be null or empty.", nameof(obfuscated));

            var parts = obfuscated.Split(new[] { "::" }, StringSplitOptions.None);

            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
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

            if (!decoded.EndsWith(salt, StringComparison.Ordinal))
                throw new InvalidOperationException("Salt mismatch or tampered data.");

            return decoded.Substring(0, decoded.Length - salt.Length);
        }

        /// <summary>
        /// Attempts to decode an obfuscated string produced by <see cref="Encode"/> without throwing on malformed or tampered input.
        /// </summary>
        /// <param name="obfuscated">String in <c>salt::base64</c> format.</param>
        /// <param name="decodedValue">When this method returns <c>true</c>, contains the decoded string; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if decoding and salt verification succeeded; otherwise <c>false</c>.</returns>
        public static bool TryDecode(string obfuscated, out string decodedValue)
        {
            decodedValue = null;
            if (string.IsNullOrEmpty(obfuscated))
            {
                return false;
            }

            try
            {
                decodedValue = Decode(obfuscated);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether <paramref name="value"/> is a valid obfuscated string that passes <see cref="TryDecode"/>.
        /// </summary>
        /// <param name="value">Candidate string to validate.</param>
        /// <returns><c>true</c> if <paramref name="value"/> decodes cleanly with a matching salt; otherwise <c>false</c>.</returns>
        public static bool IsEncoded(string value) => TryDecode(value, out _);

        /// <summary>
        /// Generates a random ASCII alphanumeric salt string using <see cref="RandomNumberGenerator"/>.
        /// </summary>
        /// <param name="length">Number of characters to generate.</param>
        /// <returns>The generated alphanumeric salt string.</returns>
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
