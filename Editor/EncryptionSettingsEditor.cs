using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using UnityEditor;
using UnityEngine;

namespace SaveEncryptionToolkit.Editor
{
    [CustomEditor(typeof(EncryptionSettings))]
    public class EncryptionSettingsEditor : UnityEditor.Editor
    {
        private GUIStyle _titleStyle;
        // Key for EditorPrefs to persist dialog state across recompiles
        private const string ShowDialogPrefKey = "SaveEncryptionToolkit.ShowKeyGenDialog";

        private void InitStyles()
        {
            _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnEnable()
        {
            if (EditorPrefs.GetBool(ShowDialogPrefKey, false))
            {
                EditorPrefs.DeleteKey(ShowDialogPrefKey);

                // Defer the dialog until the next editor update after AssetDatabase.Refresh completes.
                EditorApplication.delayCall += ShowKeyGeneratedDialogDelayed;
            }
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            var settings = (EncryptionSettings)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Encryption Key Info", _titleStyle);

            EditorGUILayout.LabelField("Obfuscated AES Key", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(settings.encryptedKey) ? "(Not generated yet)" : settings.encryptedKey,
                MessageType.None
            );

            EditorGUILayout.LabelField("Obfuscated AES IV", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(settings.encryptedIv) ? "(Not generated yet)" : settings.encryptedIv,
                MessageType.None
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Key Management", _titleStyle);

            EditorGUILayout.HelpBox(
                "Click the button below to generate a new AES Key & IV.\n" +
                "This will:\n• Obfuscate the key / IV.\n• Store them in this asset.\n• Generate a ProductionKeyStore.cs file automatically.",
                MessageType.Info
            );

            if (GUILayout.Button("Generate & Apply Secure Save Keys", GUILayout.Height(40)))
            {
                var (rawKey, rawIv) = KeyGenerator.GenerateKeyAndIv();

                Undo.RecordObject(settings, "Generate Secure Save Keys");
                settings.encryptedKey = KeyObfuscator.Encode(rawKey);
                settings.encryptedIv = KeyObfuscator.Encode(rawIv);

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                GenerateProductionKeyStoreClass(settings.encryptedKey, settings.encryptedIv);

                // Persist dialog state across the refresh/recompilation triggered by AssetDatabase.Refresh.
                EditorPrefs.SetBool(ShowDialogPrefKey, true);
                AssetDatabase.Refresh();
            }

            EditorGUILayout.Space(5);
            var hasValidKeys = settings.HasValidKeys();
            EditorGUILayout.HelpBox(
                hasValidKeys
                    ? "Key status: Valid obfuscated AES key and IV."
                    : "Key status: Missing, invalid, or unencoded key/IV. Generate keys or run Tools > HaruChanZ64 > Assets > Save Encryption Toolkit > Fix Unencoded Encryption Settings.",
                hasValidKeys ? MessageType.Info : MessageType.Error
            );

            using (new EditorGUI.DisabledScope(!hasValidKeys))
            {
                if (GUILayout.Button("Verify Keys (Round-Trip Test)", GUILayout.Height(26)))
                {
                    VerifyKeysRoundTrip(settings);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Do not commit this asset to public repositories or expose it in builds.\n" +
                "It contains sensitive encryption references used at runtime.",
                MessageType.Warning
            );
        }

        private void GenerateProductionKeyStoreClass(string encryptedKey, string encryptedIv)
        {
            var directory = ResolveRuntimeDirectory();
            var filePath = Path.Combine(directory, "ProductionKeyStore.cs");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var classContent = $@"using SaveEncryptionToolkit.Encryption;

namespace SaveEncryptionToolkit.Runtime
{{
    public static class ProductionKeyStore
    {{
        private const string _key = @""{encryptedKey}"";
        private const string _iv = @""{encryptedIv}"";

        public static string GetDecryptedKey()
        {{
            return KeyObfuscator.Decode(_key);
        }}

        public static string GetDecryptedIv()
        {{
            return KeyObfuscator.Decode(_iv);
        }}
    }}
}}
";

            File.WriteAllText(filePath, classContent);
        }

        private string ResolveRuntimeDirectory()
        {
            var script = MonoScript.FromScriptableObject(this);
            if (script != null)
            {
                var scriptPath = AssetDatabase.GetAssetPath(script);
                // Only write inside the toolkit folder if it lives under Assets/ (Packages/ is read-only in UPM)
                if (!string.IsNullOrEmpty(scriptPath) &&
                    scriptPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var editorDir = Path.GetDirectoryName(scriptPath);
                    var toolkitRoot = editorDir != null ? Path.GetDirectoryName(editorDir) : null;
                    if (!string.IsNullOrEmpty(toolkitRoot))
                        return Path.Combine(toolkitRoot, "Runtime");
                }
            }

            return "Assets/SaveEncryptionToolkit/Runtime";
        }

        private static void VerifyKeysRoundTrip(EncryptionSettings settings)
        {
            try
            {
                const string probe = "SaveEncryptionToolkit::VerificationProbe::2026";
                EncryptionUtility.Initialize(settings);
                var encrypted = EncryptionUtility.EncryptAuthenticated(probe);
                var decrypted = EncryptionUtility.DecryptAuthenticated(encrypted);

                if (decrypted == probe)
                {
                    EditorUtility.DisplayDialog(
                        "Key Verification Passed",
                        "AES + HMAC-SHA256 authenticated encryption round-trip succeeded.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Key Verification Failed",
                        "Decrypted output did not match the verification probe.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Key Verification Failed", ex.Message, "OK");
            }
        }

        private static void ShowKeyGeneratedDialogDelayed()
        {
            EditorApplication.delayCall -= ShowKeyGeneratedDialogDelayed;

            EditorUtility.DisplayDialog(
                "Keys Generated Successfully",
                "Your secure save keys have been generated and applied.",
                "OK"
            );
        }
    }
}