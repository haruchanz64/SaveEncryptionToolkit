# Save Encryption Toolkit for Unity

A flexible, encryption-ready save system toolkit for Unity. Use your own save manager while leveraging this toolkit’s robust encryption and serialization utilities. 

## Features
- **AES Encryption**: Securely encrypt save data using AES, with dynamically generated and obfuscated keys to prevent tampering and reverse-engineering.
- **Custom Save Formats**: Easily extend the system to support any custom serialization format (e.g., XML, Protocol Buffers) by implementing a simple interface.
- **Multiple Handlers Included**: Comes with out-of-the-box support for JSON and Binary serialization.
- **Editor Integration**: Streamlined workflow with custom Unity Editor tools for one-click encryption key generation and secure embedding into your project builds.
- **Obfuscated Keys**: Encryption keys are obfuscated and embedded directly into your compiled game, eliminating the need to expose sensitive ScriptableObject assets in your builds.

## Installation

### 1. Clone or Download
- **Clone**: Clone this repository and copy the `SaveEncryptionToolkit` folder into your Unity project's `Assets` directory.
- **Unity Package**: Alternatively, download the latest `.unitypackage` from the [Releases](https://github.com/haruchanz64/SaveEncryptionToolkit/releases) page and import it into your Unity project (Assets > Import Package > Custom Package...).

### 2. Create Encryption Settings
In your Unity Editor, right-click in the Project window or go to `Assets > Create > Save Encryption Toolkit > Encryption Settings`. This will create an `EncryptionSettings` ScriptableObject asset in your project. Select it in the inspector.

### 3. Generate Secure Keys
With the `EncryptionSettings` asset selected, click the "Generate & Apply Secure Save Keys" button in its Inspector window. This action will:
- Generate a new, random AES key and IV.
- Obfuscate these keys for security.
- Store the obfuscated keys within the `EncryptionSettings` asset.
- Crucially, it will automatically create or update `ProductionKeyStore.cs` in `Assets/SaveSystemToolkit/Runtime`, embedding the obfuscated keys directly into your game's compiled code for production builds.

## Save & Load Usage

The `SaveManager` class is the core of the toolkit, allowing you to easily save and load any serializable data.

### Example: Basic Save/Load with SaveManager

```csharp
using SaveEncryptionToolkit;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using UnityEngine;
using System.IO;
using System;

// Define your data structure. Mark it with [Serializable] for BinaryFormatter to work.
// For JsonUtility, public fields or [SerializeField] are sufficient.
[Serializable]
public class PlayerData
{
    public int level;
    public float health;
    public string[] inventory;
    public DateTime lastLogin; // Example of a more complex type
}

public class GameSaveSystem : MonoBehaviour
{
    private SaveManager _saveManager;
    private string _filePath;

    void Awake()
    {
        // Define your save file path. Application.persistentDataPath is recommended.
        _filePath = Path.Combine(Application.persistentDataPath, "game_save.dat");

        // 1. Initialize EncryptionUtility with the obfuscated keys from ProductionKeyStore.
        //    This uses the keys embedded in your build.
        //    IMPORTANT: Ensure you've clicked "Generate & Apply Secure Save Keys" in the editor!
        EncryptionUtility.Initialize(ProductionKeyStore.GetDecryptedKey(), ProductionKeyStore.GetDecryptedIv());

        // 2. Choose your serialization handler: JsonSaveHandler or BinarySaveHandler.
        //    You can also create your own custom handler (see "Custom Save Handlers" section).
        ISaveHandler jsonHandler = new JsonSaveHandler();
        // ISaveHandler binaryHandler = new BinarySaveHandler(); // Uncomment to use binary

        // 3. Create the SaveManager with your chosen handler.
        _saveManager = new SaveManager(jsonHandler); // Or new SaveManager(binaryHandler);

        Debug.Log($"Save file path: {_filePath}");
    }

    void Start()
    {
        // --- Saving Data ---
        var myData = new PlayerData
        {
            level = 10,
            health = 95.5f,
            inventory = new[] { "Ancient Sword", "Phoenix Shield", "Health Potion x3" },
            lastLogin = DateTime.UtcNow
        };

        try
        {
            _saveManager.Save(_filePath, myData);
            Debug.Log("Game data saved successfully!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving data: {e.Message}");
        }

        // --- Loading Data ---
        if (File.Exists(_filePath))
        {
            try
            {
                var loadedData = _saveManager.Load<PlayerData>(_filePath);
                Debug.Log("Game data loaded successfully!");
                Debug.Log($"Loaded Player Level: {loadedData.level}");
                Debug.Log($"Loaded Player Health: {loadedData.health}");
                Debug.Log($"Loaded Inventory: {string.Join(", ", loadedData.inventory)}");
                Debug.Log($"Last Login: {loadedData.lastLogin.ToLocalTime()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading data: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("No save file found. Start a new game!");
        }
    }

    // Example of deleting a save file
    public void DeleteSave()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.Log("No save file to delete.");
        }
    }
}
```

## Customization

### Custom Save Handlers
The Save Encryption Toolkit is designed to be extensible. You can easily implement your own serialization logic by creating a class that implements the `ISaveHandler` interface. This allows you to use any serialization library or format you prefer (e.g., XML, Protobuf, custom binary formats).

1. Create a new C# script (e.g., `XmlSaveHandler.cs`).
2. Implement `ISaveHandler`:

```csharp
using SaveEncryptionToolkit.Handlers;
// Add your custom serialization library using directives here

public class XmlSaveHandler : ISaveHandler
{
    public string Serialize<T>(T data)
    {
        // Implement your XML serialization logic here
        // Example: return MyXmlSerializer.Serialize(data);
        throw new System.NotImplementedException();
    }

    public T Deserialize<T>(string data)
    {
        // Implement your XML deserialization logic here
        // Example: return MyXmlSerializer.Deserialize<T>(data);
        throw new System.NotImplementedException();
    }
}
```

3. Use your custom handler:

```csharp
ISaveHandler customHandler = new XmlSaveHandler();
_saveManager = new SaveManager(customHandler);
```

## Troubleshooting: Fixing Legacy or Unencoded Keys

If you encounter issues due to manually edited or legacy ``EncryptionSettings assets`` (e.g., missing the :: delimiter in obfuscated keys), you can easily repair them using the built-in fixer tool.

How to Fix

Go to:

``Tools > Save Encryption Toolkit > Fix Unencoded Encryption Settings``

This will:

- Scan your project for all EncryptionSettings assets.
- Detect improperly encoded AES keys or IVs.
- Automatically re-encode and fix them using the internal KeyObfuscator.

## Important Notes
- **EncryptionSettings Security**: The `EncryptionSettings.asset` file in your project contains the unobfuscated (but still encoded) keys for editor use only. Do NOT include this asset in your final game builds or commit it to public repositories without proper `.gitignore` rules. The `ProductionKeyStore.cs` generated by the editor tool is designed for secure, embedded key usage in builds.
- **Regenerate Keys**: If you ever suspect your encryption keys might be compromised, simply open your `EncryptionSettings` asset in the Unity Editor and click "Generate & Apply Secure Save Keys" again to create entirely new obfuscated keys.
- **BinaryFormatter Obsoletion**: The `BinarySaveHandler` utilizes `BinaryFormatter`, which is marked as obsolete by Microsoft due to security concerns. While still functional in Unity, it's recommended to prefer `JsonSaveHandler` or implement a custom handler with a more modern and secure serialization method for new projects.

## License
This project is released under the [MIT License](https://github.com/haruchanz64/SaveEncryptionToolkit/blob/main/LICENSE). You are free to use, modify, and distribute this software for personal and commercial purposes.