using SaveEncryptionToolkit.Encryption;
using UnityEditor;
using UnityEngine;

namespace SaveEncryptionToolkit.Editor
{
    /// <summary>
    /// Editor utility that scans the project for <see cref="EncryptionSettings"/> assets and re-encodes any raw key or IV fields with <see cref="KeyObfuscator"/>.
    /// </summary>
    public static class EncryptionFixer
    {
        /// <summary>
        /// Menu command that finds all <see cref="EncryptionSettings"/> assets in the project and obfuscates any non-empty key or IV values that fail <see cref="KeyObfuscator.IsEncoded"/>.
        /// </summary>
        [MenuItem("Tools/HaruChanZ64/Assets/Save Encryption Toolkit/Fix Unencoded Encryption Settings", false, 152)]
        public static void FixEncryptionAsset()
        {
            var guids = AssetDatabase.FindAssets("t:EncryptionSettings");

            if (guids.Length == 0)
            {
                Debug.LogWarning("[EncryptionFixer] No EncryptionSettings asset found.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<EncryptionSettings>(path);

                if (settings == null)
                {
                    Debug.LogWarning($"[EncryptionFixer] Failed to load EncryptionSettings at {path}");
                    continue;
                }

                var modified = false;

                if (!string.IsNullOrEmpty(settings.encryptedKey) && !KeyObfuscator.IsEncoded(settings.encryptedKey))
                {
                    Debug.Log($"[EncryptionFixer] Re-encoding key in {path}");
                    settings.encryptedKey = KeyObfuscator.Encode(settings.encryptedKey);
                    modified = true;
                }

                if (!string.IsNullOrEmpty(settings.encryptedIv) && !KeyObfuscator.IsEncoded(settings.encryptedIv))
                {
                    Debug.Log($"[EncryptionFixer] Re-encoding IV in {path}");
                    settings.encryptedIv = KeyObfuscator.Encode(settings.encryptedIv);
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(settings);
                    Debug.Log($"[EncryptionFixer] Fixed and updated: {path}");
                }
                else
                {
                    Debug.Log($"[EncryptionFixer] Already encoded or empty: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[EncryptionFixer] Scan complete.");
        }
    }
}