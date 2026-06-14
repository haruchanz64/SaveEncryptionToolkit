using System;
using NUnit.Framework;
using SaveEncryptionToolkit.Encryption;

namespace SaveEncryptionToolkit.Tests
{
    public sealed class KeyObfuscatorAndGeneratorTests
    {
        [Test]
        public void GenerateKeyAndIv_ProducesValidAes256KeyAnd128BitIv()
        {
            var (key1, iv1) = KeyGenerator.GenerateKeyAndIv();
            var (key2, iv2) = KeyGenerator.GenerateKeyAndIv();

            Assert.AreEqual(32, Convert.FromBase64String(key1).Length);
            Assert.AreEqual(16, Convert.FromBase64String(iv1).Length);
            Assert.AreNotEqual(key1, key2);
            Assert.AreNotEqual(iv1, iv2);
        }

        [Test]
        public void KeyObfuscator_RoundTripsAndDetectsTampering()
        {
            var (rawKey, _) = KeyGenerator.GenerateKeyAndIv();
            var encoded = KeyObfuscator.Encode(rawKey);

            Assert.IsTrue(KeyObfuscator.IsEncoded(encoded));
            Assert.IsFalse(KeyObfuscator.IsEncoded(rawKey));
            Assert.AreEqual(rawKey, KeyObfuscator.Decode(encoded));

            // Tampering with salt prefix should fail salt verification.
            var tampered = "X" + encoded.Substring(1);
            Assert.IsFalse(KeyObfuscator.IsEncoded(tampered));
            Assert.Throws<InvalidOperationException>(() => KeyObfuscator.Decode(tampered));
            Assert.Throws<FormatException>(() => KeyObfuscator.Decode("missing_delimiter"));
        }
    }
}
