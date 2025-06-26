using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using UnityEngine;

namespace SaveEncryptionToolkit.Samples
{
    [Serializable]
    public sealed class SamplePlayerSaveData
    {
        public string playerName = "Adventurer";
        public int level = 1;
        public int gold = 250;
    }

    /// <summary>
    /// Example MonoBehaviour combining EncryptionSettings, EncryptionUtility, and JsonSaveHandler
    /// to write and read an AES-encrypted save file under Application.persistentDataPath.
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
