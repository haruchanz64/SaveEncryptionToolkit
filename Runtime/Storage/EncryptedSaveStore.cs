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

        /// <summary>
        /// Creates a new encrypted file store with the specified serializer, encryption mode, and backup behavior.
        /// </summary>
        /// <param name="handler">Serialization handler used for save payloads. Defaults to <see cref="JsonSaveHandler"/> when <c>null</c>.</param>
        /// <param name="useAuthenticatedEncryption">If <c>true</c>, uses <see cref="EncryptionUtility.EncryptAuthenticated"/> and <see cref="EncryptionUtility.DecryptAuthenticated"/>; otherwise uses legacy static-IV AES.</param>
        /// <param name="maintainBackup">If <c>true</c>, rotates the prior save file to a <c>.bak</c> copy before overwriting and falls back to it on read failure.</param>
        public EncryptedSaveStore(
            ISaveHandler handler = null,
            bool useAuthenticatedEncryption = true,
            bool maintainBackup = true)
        {
            _handler = handler ?? new JsonSaveHandler();
            _useAuthenticatedEncryption = useAuthenticatedEncryption;
            _maintainBackup = maintainBackup;
        }

        /// <summary>
        /// Returns the backup file path corresponding to <paramref name="filePath"/> by appending <c>.bak</c>.
        /// </summary>
        /// <param name="filePath">Primary save file path.</param>
        /// <returns>The derived backup file path (<c>&lt;filePath&gt;.bak</c>).</returns>
        public static string GetBackupPath(string filePath) => filePath + ".bak";

        /// <summary>
        /// Serializes, encrypts, and writes <paramref name="data"/> to <paramref name="filePath"/> using a temporary <c>.tmp</c> file and optional <c>.bak</c> rotation.
        /// Creates missing parent directories automatically.
        /// </summary>
        /// <typeparam name="T">Type of the save object being written.</typeparam>
        /// <param name="filePath">Destination file path on disk. Must not be <c>null</c> or whitespace.</param>
        /// <param name="data">Save object instance to persist. Must not be <c>null</c>.</param>
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

            // Stage to .tmp before rotating the existing file so an interrupted write does not corrupt the active save.
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

        /// <summary>
        /// Reads, decrypts, and deserializes a save object from <paramref name="filePath"/>, automatically falling back to the <c>.bak</c> file when backup recovery is enabled and the primary file fails to load.
        /// </summary>
        /// <typeparam name="T">Target save object type.</typeparam>
        /// <param name="filePath">Primary save file path. Must not be <c>null</c> or whitespace.</param>
        /// <returns>The deserialized save object.</returns>
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

        /// <summary>
        /// Attempts to load and deserialize a save object from <paramref name="filePath"/> (or its <c>.bak</c> fallback) without throwing on failure.
        /// </summary>
        /// <typeparam name="T">Target save object type.</typeparam>
        /// <param name="filePath">Primary save file path.</param>
        /// <param name="data">When this method returns <c>true</c>, contains the loaded object; otherwise the default value of <typeparamref name="T"/>.</param>
        /// <returns><c>true</c> if the primary or backup file loaded cleanly; otherwise <c>false</c>.</returns>
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

        /// <summary>
        /// Checks whether a primary save file exists at <paramref name="filePath"/>, or whether a <c>.bak</c> file exists when backup recovery is enabled.
        /// </summary>
        /// <param name="filePath">Primary save file path.</param>
        /// <returns><c>true</c> if a readable primary or backup file path exists on disk; otherwise <c>false</c>.</returns>
        public bool Exists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            return File.Exists(filePath) || (_maintainBackup && File.Exists(GetBackupPath(filePath)));
        }

        /// <summary>
        /// Deletes the primary save file, any leftover <c>.tmp</c> staging file, and optionally the <c>.bak</c> backup file.
        /// </summary>
        /// <param name="filePath">Primary save file path. No action is taken if <c>null</c> or whitespace.</param>
        /// <param name="deleteBackup">If <c>true</c>, also removes the <c>.bak</c> file if present.</param>
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

        /// <summary>
        /// Reads raw text from <paramref name="path"/>, decrypts it using the configured encryption mode, and deserializes the result with the active <see cref="ISaveHandler"/>.
        /// </summary>
        /// <typeparam name="T">Target save object type.</typeparam>
        /// <param name="path">File path to read from disk.</param>
        /// <returns>The deserialized save object.</returns>
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
