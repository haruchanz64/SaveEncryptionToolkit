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

        // OnEnable is called when the object is enabled in the inspector.
        // This includes after a script recompile or AssetDatabase.Refresh().
        private void OnEnable()
        {
            // Check if the flag was set before a refresh/recompile
            if (EditorPrefs.GetBool(ShowDialogPrefKey, false))
            {
                // Clear the flag immediately to prevent showing it again
                EditorPrefs.DeleteKey(ShowDialogPrefKey);

                // Use delayCall to show the dialog on the next editor update,
                // ensuring it happens after the AssetDatabase.Refresh() completes.
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

                // Set the EditorPrefs flag to indicate dialog should be shown after refresh
                EditorPrefs.SetBool(ShowDialogPrefKey, true);

                // AssetDatabase.Refresh() will trigger OnEnable again potentially after recompilation
                AssetDatabase.Refresh();
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

        // This method will be called via EditorApplication.delayCall
        private static void ShowKeyGeneratedDialogDelayed()
        {
            // Remove the delegate to prevent it from being called multiple times
            EditorApplication.delayCall -= ShowKeyGeneratedDialogDelayed;

            // Display the dialog
            EditorUtility.DisplayDialog(
                "Keys Generated Successfully",
                "Your secure save keys have been generated and applied.",
                "OK"
            );
        }
    }
}