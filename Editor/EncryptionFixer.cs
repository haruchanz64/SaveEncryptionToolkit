using SaveEncryptionToolkit.Encryption;
using UnityEditor;
using UnityEngine;

namespace SaveEncryptionToolkit.Editor
{
    public static class EncryptionFixer
    {
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