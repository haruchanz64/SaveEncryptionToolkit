using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using UnityEngine;

namespace SaveEncryptionToolkit.Samples
{
    /// <summary>
    /// Serializable sample payload persisted by <see cref="SampleEncryptedSaveManager"/>.
    /// </summary>
    [Serializable]
    public sealed class SamplePlayerSaveData
    {
        /// <summary>
        /// Sample player display name.
        /// </summary>
        public string playerName = "Adventurer";

        /// <summary>
        /// Sample character level.
        /// </summary>
        public int level = 1;

        /// <summary>
        /// Sample gold balance.
        /// </summary>
        public int gold = 250;
    }

    /// <summary>
    /// Example <see cref="MonoBehaviour"/> combining <see cref="EncryptionSettings"/>, <see cref="EncryptionUtility"/>, and <see cref="JsonSaveHandler"/>
    /// to write and read an AES-encrypted save file under <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public sealed class SampleEncryptedSaveManager : MonoBehaviour
    {
        [SerializeField] private EncryptionSettings encryptionSettings;
        [SerializeField] private string fileName = "player_save.enc";
        [SerializeField] private SamplePlayerSaveData currentSave = new SamplePlayerSaveData();

        private readonly ISaveHandler _serializer = new JsonSaveHandler();

        private string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        private void Awake()
        {
            if (encryptionSettings != null)
            {
                EncryptionUtility.Initialize(encryptionSettings);
            }
        }

        /// <summary>
        /// Serializes <c>currentSave</c> to JSON, encrypts it with <see cref="EncryptionUtility.Encrypt"/>, and writes the ciphertext to disk.
        /// </summary>
        [ContextMenu("Save Encrypted Data")]
        public void Save()
        {
            if (encryptionSettings != null)
            {
                EncryptionUtility.Initialize(encryptionSettings);
            }

            string plainJson = _serializer.Serialize(currentSave);
            string cipherText = EncryptionUtility.Encrypt(plainJson);
            File.WriteAllText(SavePath, cipherText);
            Debug.Log($"[SampleEncryptedSaveManager] Saved encrypted file to {SavePath}");
        }

        /// <summary>
        /// Reads the encrypted file from disk, decrypts it with <see cref="EncryptionUtility.Decrypt"/>, and deserializes it into <c>currentSave</c>.
        /// </summary>
        [ContextMenu("Load Encrypted Data")]
        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning($"[SampleEncryptedSaveManager] Save file not found at {SavePath}");
                return;
            }

            if (encryptionSettings != null)
            {
                EncryptionUtility.Initialize(encryptionSettings);
            }

            string cipherText = File.ReadAllText(SavePath);
            string plainJson = EncryptionUtility.Decrypt(cipherText);
            currentSave = _serializer.Deserialize<SamplePlayerSaveData>(plainJson);
            Debug.Log($"[SampleEncryptedSaveManager] Loaded save: {currentSave.playerName} (Lv.{currentSave.level}, {currentSave.gold}G)");
        }
    }
}
