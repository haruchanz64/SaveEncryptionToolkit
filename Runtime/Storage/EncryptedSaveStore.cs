using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;

namespace SaveEncryptionToolkit.Storage
{
    /// <summary>
    /// High-level encrypted file store combining any <see cref="ISaveHandler"/> with <see cref="EncryptionUtility"/>,
    /// atomic temp-file writes, and optional .bak recovery if the primary file is corrupted or tampered with.
    /// </summary>
    public sealed class EncryptedSaveStore
    {
        private readonly ISaveHandler _handler;
        private readonly bool _useAuthenticatedEncryption;
        private readonly bool _maintainBackup;

        public EncryptedSaveStore(
            ISaveHandler handler = null,
            bool useAuthenticatedEncryption = true,
            bool maintainBackup = true)
        {
            _handler = handler ?? new JsonSaveHandler();
            _useAuthenticatedEncryption = useAuthenticatedEncryption;
            _maintainBackup = maintainBackup;
        }

        public static string GetBackupPath(string filePath) => filePath + ".bak";

        public void Save<T>(string filePath, T data)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Save file path cannot be null or whitespace.", nameof(filePath));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serialized = _handler.Serialize(data);
            var payload = _useAuthenticatedEncryption
                ? EncryptionUtility.EncryptAuthenticated(serialized)
                : EncryptionUtility.Encrypt(serialized);

            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, payload);

            var backupPath = GetBackupPath(filePath);
            if (File.Exists(filePath))
            {
                if (_maintainBackup)
                {
                    File.Copy(filePath, backupPath, overwrite: true);
                }

                File.Delete(filePath);
            }

            File.Move(tempPath, filePath);
        }

        public T Load<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Save file path cannot be null or whitespace.", nameof(filePath));

            Exception primaryError = null;
            if (File.Exists(filePath))
            {
                try
                {
                    return ReadAndDeserialize<T>(filePath);
                }
                catch (Exception ex)
                {
                    primaryError = ex;
                }
            }

            var backupPath = GetBackupPath(filePath);
            if (_maintainBackup && File.Exists(backupPath))
            {
                return ReadAndDeserialize<T>(backupPath);
            }

            if (primaryError != null)
            {
                throw primaryError;
            }

            throw new FileNotFoundException("Save file not found.", filePath);
        }

        public bool TryLoad<T>(string filePath, out T data)
        {
            data = default;
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                data = Load<T>(filePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Exists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            return File.Exists(filePath) || (_maintainBackup && File.Exists(GetBackupPath(filePath)));
        }

        public void Delete(string filePath, bool deleteBackup = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            if (File.Exists(filePath))
                File.Delete(filePath);

            var tempPath = filePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            if (deleteBackup)
            {
                var backupPath = GetBackupPath(filePath);
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
        }

        private T ReadAndDeserialize<T>(string path)
        {
            var payload = File.ReadAllText(path);
            var serialized = _useAuthenticatedEncryption
                ? EncryptionUtility.DecryptAuthenticated(payload)
                : EncryptionUtility.Decrypt(payload);

            return _handler.Deserialize<T>(serialized);
        }
    }
}
