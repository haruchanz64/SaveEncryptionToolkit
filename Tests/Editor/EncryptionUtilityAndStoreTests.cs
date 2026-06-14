using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using SaveEncryptionToolkit.Storage;

namespace SaveEncryptionToolkit.Tests
{
    public sealed class EncryptionUtilityAndStoreTests
    {
        [Serializable]
        private sealed class TestSavePayload
        {
            public string playerName;
            public int level;
            public float health;
        }

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            EncryptionUtility.Reset();
            var (key, iv) = KeyGenerator.GenerateKeyAndIv();
            EncryptionUtility.Initialize(key, iv);

            _tempDir = Path.Combine(Path.GetTempPath(), "SaveEncryptionToolkitTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            EncryptionUtility.Reset();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void LegacyEncryptDecrypt_RoundTripsString()
        {
            const string original = "{\"level\":42,\"name\":\"Haru\"}";
            var cipher = EncryptionUtility.Encrypt(original);
            Assert.AreNotEqual(original, cipher);
            Assert.AreEqual(original, EncryptionUtility.Decrypt(cipher));
        }

        [Test]
        public void AuthenticatedEncryption_UsesRandomIvAndRejectsTampering()
        {
            const string original = "Sensitive save state with UTF-8: 日本語 / 100%";

            var payload1 = EncryptionUtility.EncryptAuthenticated(original);
            var payload2 = EncryptionUtility.EncryptAuthenticated(original);

            // Random IV guarantees distinct ciphertexts for identical plaintexts.
            Assert.AreNotEqual(payload1, payload2);
            Assert.AreEqual(original, EncryptionUtility.DecryptAuthenticated(payload1));
            Assert.AreEqual(original, EncryptionUtility.DecryptAuthenticated(payload2));

            // Flip a single bit in the ciphertext portion and assert HMAC rejection.
            var bytes = Convert.FromBase64String(payload1);
            bytes[22] ^= 0x01;
            var tampered = Convert.ToBase64String(bytes);

            Assert.Throws<CryptographicException>(() => EncryptionUtility.DecryptAuthenticated(tampered));
            Assert.IsFalse(EncryptionUtility.TryDecryptAuthenticated(tampered, out _));
        }

        [Test]
        public void EncryptedSaveStore_SavesLoadsRecoversFromBackupAndDeletes()
        {
            var store = new EncryptedSaveStore(new JsonSaveHandler(), useAuthenticatedEncryption: true, maintainBackup: true);
            var savePath = Path.Combine(_tempDir, "slot1.sav");

            var firstSave = new TestSavePayload { playerName = "Aron", level = 5, health = 80.5f };
            store.Save(savePath, firstSave);

            Assert.IsTrue(store.Exists(savePath));
            var loadedFirst = store.Load<TestSavePayload>(savePath);
            Assert.AreEqual("Aron", loadedFirst.playerName);
            Assert.AreEqual(5, loadedFirst.level);

            // Second save rotates first save into .bak.
            var secondSave = new TestSavePayload { playerName = "Aron", level = 12, health = 99.0f };
            store.Save(savePath, secondSave);
            Assert.IsTrue(File.Exists(EncryptedSaveStore.GetBackupPath(savePath)));
            Assert.AreEqual(12, store.Load<TestSavePayload>(savePath).level);

            // Corrupt primary file; store should transparently fall back to valid .bak.
            File.WriteAllText(savePath, "CORRUPTED_DATA!!!");
            var recovered = store.Load<TestSavePayload>(savePath);
            Assert.AreEqual(5, recovered.level);

            store.Delete(savePath);
            Assert.IsFalse(store.Exists(savePath));
        }

        [Test]
        public void Reset_ClearsKeyStateAndThrowsUntilReinitialized()
        {
            Assert.IsTrue(EncryptionUtility.IsInitialized);
            EncryptionUtility.Reset();
            Assert.IsFalse(EncryptionUtility.IsInitialized);
            Assert.Throws<InvalidOperationException>(() => EncryptionUtility.EncryptAuthenticated("test"));
        }
    }
}
