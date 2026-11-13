using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveEncryptionToolkit.Encryption
{
    /// <summary>
    /// Static AES encryption utility supporting versioned Encrypt-then-MAC (AES-CBC + HMAC-SHA256) envelopes and legacy static-IV AES-CBC payloads.
    /// </summary>
    public static class EncryptionUtility
    {
        private static readonly byte[] AuthenticatedMagic = { 0x53, 0x45, 0x54, 0x31 }; // "SET1"
        private static readonly byte[] MacKeyDomain = Encoding.UTF8.GetBytes("SET1-HMAC-SHA256");
        private const int IvLength = 16;
        private const int MacLength = 32;

        private static byte[] _key;
        private static byte[] _iv;
        private static byte[] _macKey;

        /// <summary>
        /// Gets a value indicating whether an AES key, IV, and derived HMAC key are currently loaded in memory.
        /// </summary>
        public static bool IsInitialized => _key != null && _iv != null && _macKey != null;

        // Clears cached static key material across Enter Play Mode domain reloads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Reset();
        }

        /// <summary>
        /// Zeroes out in-memory AES key, IV, and derived MAC key buffers and resets <see cref="IsInitialized"/> to <c>false</c>.
        /// </summary>
        public static void Reset()
        {
            if (_key != null) Array.Clear(_key, 0, _key.Length);
            if (_iv != null) Array.Clear(_iv, 0, _iv.Length);
            if (_macKey != null) Array.Clear(_macKey, 0, _macKey.Length);
            _key = null;
            _iv = null;
            _macKey = null;
        }

        /// <summary>
        /// Initializes the utility by decoding the obfuscated AES key and IV from an <see cref="EncryptionSettings"/> asset.
        /// </summary>
        /// <param name="encryptionSettings">Asset containing obfuscated AES key and IV strings. Must not be <c>null</c>.</param>
        public static void Initialize(EncryptionSettings encryptionSettings)
        {
            if (encryptionSettings == null)
                throw new ArgumentNullException(nameof(encryptionSettings));

            Initialize(encryptionSettings.GetEncryptedKey(), encryptionSettings.GetEncryptedIvKey());
        }

        /// <summary>
        /// Initializes the utility from Base64-encoded AES key and IV strings.
        /// </summary>
        /// <param name="base64Key">Base64-encoded 128-, 192-, or 256-bit AES key.</param>
        /// <param name="base64Iv">Base64-encoded 128-bit AES IV used by legacy static-IV methods.</param>
        public static void Initialize(string base64Key, string base64Iv)
        {
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentException("AES key cannot be null or empty.", nameof(base64Key));

            if (string.IsNullOrEmpty(base64Iv))
                throw new ArgumentException("AES IV cannot be null or empty.", nameof(base64Iv));

            var key = Convert.FromBase64String(base64Key);
            var iv = Convert.FromBase64String(base64Iv);
            Initialize(key, iv);
        }

        /// <summary>
        /// Initializes the utility from raw AES key and IV byte arrays, defensively cloning both buffers and deriving a separate HMAC-SHA256 key.
        /// </summary>
        /// <param name="key">Raw AES key bytes; must be 16, 24, or 32 bytes long.</param>
        /// <param name="iv">Raw AES IV bytes used by legacy static-IV encryption; must be 16 bytes long.</param>
        public static void Initialize(byte[] key, byte[] iv)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (iv == null)
                throw new ArgumentNullException(nameof(iv));

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 128, 192, or 256 bits (16, 24, or 32 bytes).", nameof(key));

            if (iv.Length != IvLength)
                throw new ArgumentException("AES IV must be 128 bits (16 bytes).", nameof(iv));

            _key = (byte[])key.Clone();
            _iv = (byte[])iv.Clone();
            _macKey = DeriveMacKey(_key);
        }

        /// <summary>
        /// Encrypts plain text using the configured static IV (legacy format). Prefer <see cref="EncryptAuthenticated"/> for new saves.
        /// </summary>
        /// <param name="plainText">UTF-8 plain text to encrypt. Must not be <c>null</c>.</param>
        /// <returns>Base64-encoded AES-CBC ciphertext.</returns>
        public static string Encrypt(string plainText)
        {
            EnsureInitialized();

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

        /// <summary>
        /// Decrypts cipher text produced by <see cref="Encrypt"/>.
        /// </summary>
        /// <param name="cipherText">Base64-encoded AES-CBC ciphertext. Must not be <c>null</c> or empty.</param>
        /// <returns>The decrypted UTF-8 plain text.</returns>
        public static string Decrypt(string cipherText)
        {
            EnsureInitialized();

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

        /// <summary>
        /// Attempts to decrypt legacy cipher text produced by <see cref="Encrypt"/> without throwing on invalid input or uninitialized state.
        /// </summary>
        /// <param name="cipherText">Base64-encoded legacy AES ciphertext.</param>
        /// <param name="plainText">When this method returns <c>true</c>, contains the decrypted UTF-8 string; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if decryption succeeded; otherwise <c>false</c>.</returns>
        public static bool TryDecrypt(string cipherText, out string plainText)
        {
            plainText = null;
            if (!IsInitialized || string.IsNullOrEmpty(cipherText))
                return false;

            try
            {
                plainText = Decrypt(cipherText);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Encrypts plain text with a fresh random 128-bit IV and an HMAC-SHA256 authentication tag (Encrypt-then-MAC).
        /// </summary>
        /// <param name="plainText">UTF-8 plain text to encrypt. Must not be <c>null</c>.</param>
        /// <returns>Base64-encoded <c>SET1</c> authenticated envelope.</returns>
        public static string EncryptAuthenticated(string plainText)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var envelope = EncryptBytes(plainBytes);
            return Convert.ToBase64String(envelope);
        }

        /// <summary>
        /// Verifies the HMAC-SHA256 tag and decrypts a payload produced by <see cref="EncryptAuthenticated"/>.
        /// Throws <see cref="CryptographicException"/> if the payload has been tampered with or corrupted.
        /// </summary>
        /// <param name="authenticatedPayload">Base64-encoded <c>SET1</c> authenticated envelope.</param>
        /// <returns>The verified and decrypted UTF-8 plain text.</returns>
        public static string DecryptAuthenticated(string authenticatedPayload)
        {
            if (string.IsNullOrEmpty(authenticatedPayload))
                throw new ArgumentException("Authenticated payload cannot be null or empty.", nameof(authenticatedPayload));

            var envelope = Convert.FromBase64String(authenticatedPayload);
            var plainBytes = DecryptBytes(envelope);
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Attempts to verify and decrypt an authenticated payload produced by <see cref="EncryptAuthenticated"/> without throwing on failure.
        /// </summary>
        /// <param name="authenticatedPayload">Base64-encoded <c>SET1</c> authenticated envelope.</param>
        /// <param name="plainText">When this method returns <c>true</c>, contains the verified UTF-8 plain text; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if HMAC verification and decryption succeeded; otherwise <c>false</c>.</returns>
        public static bool TryDecryptAuthenticated(string authenticatedPayload, out string plainText)
        {
            plainText = null;
            if (!IsInitialized || string.IsNullOrEmpty(authenticatedPayload))
                return false;

            try
            {
                plainText = DecryptAuthenticated(authenticatedPayload);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Encrypts raw bytes into a versioned authenticated envelope: [SET1 (4B)][Random IV (16B)][Ciphertext][HMAC-SHA256 (32B)].
        /// </summary>
        /// <param name="plainBytes">Raw plaintext bytes to encrypt. Must not be <c>null</c>.</param>
        /// <returns>The versioned binary envelope containing the magic header, random 16-byte IV, AES-CBC ciphertext, and 32-byte HMAC-SHA256 tag.</returns>
        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            EnsureInitialized();

            if (plainBytes == null)
                throw new ArgumentNullException(nameof(plainBytes));

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();
            var randomIv = aes.IV;

            byte[] cipherBytes;
            using (var encryptor = aes.CreateEncryptor())
            {
                cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }

            var signedLength = AuthenticatedMagic.Length + IvLength + cipherBytes.Length;
            var envelope = new byte[signedLength + MacLength];

            Buffer.BlockCopy(AuthenticatedMagic, 0, envelope, 0, AuthenticatedMagic.Length);
            Buffer.BlockCopy(randomIv, 0, envelope, AuthenticatedMagic.Length, IvLength);
            Buffer.BlockCopy(cipherBytes, 0, envelope, AuthenticatedMagic.Length + IvLength, cipherBytes.Length);

            // Compute HMAC over header + IV + ciphertext (Encrypt-then-MAC).
            using var hmac = new HMACSHA256(_macKey);
            var mac = hmac.ComputeHash(envelope, 0, signedLength);
            Buffer.BlockCopy(mac, 0, envelope, signedLength, MacLength);

            return envelope;
        }

        /// <summary>
        /// Verifies the header and HMAC-SHA256 tag in constant time, then decrypts a versioned envelope produced by <see cref="EncryptBytes"/>.
        /// </summary>
        /// <param name="envelope">Binary <c>SET1</c> authenticated envelope.</param>
        /// <returns>The verified and decrypted plaintext bytes.</returns>
        public static byte[] DecryptBytes(byte[] envelope)
        {
            EnsureInitialized();

            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var minLength = AuthenticatedMagic.Length + IvLength + 1 + MacLength;
            if (envelope.Length < minLength)
                throw new CryptographicException("Encrypted payload is truncated or invalid.");

            for (var i = 0; i < AuthenticatedMagic.Length; i++)
            {
                if (envelope[i] != AuthenticatedMagic[i])
                    throw new CryptographicException("Unsupported or corrupted encrypted payload header.");
            }

            // Always verify the MAC before touching AES decryption to prevent padding-oracle attacks.
            var signedLength = envelope.Length - MacLength;
            using var hmac = new HMACSHA256(_macKey);
            var expectedMac = hmac.ComputeHash(envelope, 0, signedLength);

            if (!FixedTimeEquals(expectedMac, 0, envelope, signedLength, MacLength))
                throw new CryptographicException("Save payload failed integrity verification (HMAC mismatch).");

            var iv = new byte[IvLength];
            Buffer.BlockCopy(envelope, AuthenticatedMagic.Length, iv, 0, IvLength);

            var cipherOffset = AuthenticatedMagic.Length + IvLength;
            var cipherLength = signedLength - cipherOffset;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(envelope, cipherOffset, cipherLength);
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if <see cref="IsInitialized"/> is <c>false</c>.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("EncryptionUtility is not initialized. Call Initialize() first.");
        }

        /// <summary>
        /// Derives a dedicated 256-bit HMAC key from the AES key using SHA-256 with a fixed domain separator so encryption and authentication do not reuse the same raw key.
        /// </summary>
        /// <param name="key">Raw AES key bytes.</param>
        /// <returns>A 32-byte domain-separated HMAC key.</returns>
        private static byte[] DeriveMacKey(byte[] key)
        {
            var material = new byte[key.Length + MacKeyDomain.Length];
            Buffer.BlockCopy(key, 0, material, 0, key.Length);
            Buffer.BlockCopy(MacKeyDomain, 0, material, key.Length, MacKeyDomain.Length);

            using var sha = SHA256.Create();
            return sha.ComputeHash(material);
        }

        /// <summary>
        /// Compares two byte ranges in constant time via bitwise XOR accumulation to avoid timing side channels during MAC verification.
        /// </summary>
        /// <param name="a">First byte array.</param>
        /// <param name="aOffset">Starting offset in <paramref name="a"/>.</param>
        /// <param name="b">Second byte array.</param>
        /// <param name="bOffset">Starting offset in <paramref name="b"/>.</param>
        /// <param name="length">Number of bytes to compare.</param>
        /// <returns><c>true</c> if all bytes in the compared slices match; otherwise <c>false</c>.</returns>
        private static bool FixedTimeEquals(byte[] a, int aOffset, byte[] b, int bOffset, int length)
        {
            var diff = 0;
            for (var i = 0; i < length; i++)
            {
                diff |= a[aOffset + i] ^ b[bOffset + i];
            }

            return diff == 0;
        }
    }
}
