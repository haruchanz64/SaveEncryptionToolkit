using UnityEditor;
using UnityEngine;

namespace SaveEncryptionToolkit.Encryption
{
    public static class EncryptionFixer
    {
        [MenuItem("Tools/Save Encryption Toolkit/Fix Unencoded Encryption Settings")]
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

                var modified = false;

                if (!settings.encryptedKey.Contains("::"))
                {
                    Debug.Log($"[EncryptionFixer] Re-encoding key in {path}");
                    settings.encryptedKey = KeyObfuscator.Encode(settings.encryptedKey);
                    modified = true;
                }

                if (!settings.encryptedIv.Contains("::"))
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
                    Debug.Log($"[EncryptionFixer] Already encoded: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[EncryptionFixer] Scan complete.");
        }
    }
}