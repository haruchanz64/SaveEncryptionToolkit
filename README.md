# Save Encryption Toolkit

Encryption-ready save system utilities for Unity 6 that decouple storage security and integrity verification from your save schemas.

## Features

- **Authenticated Encryption (AES-CBC + HMAC-SHA256)**: `EncryptAuthenticated` / `DecryptAuthenticated` and `EncryptBytes` / `DecryptBytes` generate a fresh random 128-bit IV per write and verify integrity in constant time before decrypting.
- **Atomic File Storage & Backup Recovery**: `EncryptedSaveStore` writes via `.tmp` files, rotates prior saves to `.bak`, and automatically falls back to `.bak` if the primary save is corrupted or tampered with.
- **Pluggable Serialization**: Implement `ISaveHandler` for any format, or use the built-in `JsonSaveHandler` (recommended) or legacy `BinarySaveHandler`.
- **Editor Key Generation & Verification**: Generate obfuscated keys from `EncryptionSettings`, embed them into `ProductionKeyStore.cs`, and run round-trip verification directly from the Inspector.
- **Legacy Compatibility**: Retains `EncryptionUtility.Encrypt` / `Decrypt` for existing save files.

## Installation

### Unity Package Manager (Git URL)
1. Open **Window > Package Manager** in Unity 6.
2. Click **+** > **Add package from git URL...**
3. Enter:
   ```text
   https://github.com/haruchanz64/SaveEncryptionToolkit.git
   ```

### Key Setup
1. Create an asset via **Assets > Create > Save Encryption Toolkit > Encryption Settings**.
2. Click **Generate & Apply Secure Save Keys** to generate obfuscated keys and emit `ProductionKeyStore.cs` under `Assets/SaveEncryptionToolkit/Runtime`.
3. Click **Verify Keys (Round-Trip Test)** to confirm the key pair passes authenticated round-trip encryption.

## Usage

```csharp
using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using SaveEncryptionToolkit.Runtime;
using SaveEncryptionToolkit.Storage;
using UnityEngine;

[Serializable]
public class PlayerData
{
    public int level;
    public float health;
    public string[] inventory;
}

public class GameSaveSystem : MonoBehaviour
{
    private EncryptedSaveStore _store;
    private string _filePath;

    private void Awake()
    {
        _filePath = Path.Combine(Application.persistentDataPath, "game_save.dat");

        EncryptionUtility.Initialize(
            ProductionKeyStore.GetDecryptedKey(),
            ProductionKeyStore.GetDecryptedIv());

        _store = new EncryptedSaveStore(
            handler: new JsonSaveHandler(),
            useAuthenticatedEncryption: true,
            maintainBackup: true);
    }

    public void Save(PlayerData data) => _store.Save(_filePath, data);

    public bool TryLoad(out PlayerData data) => _store.TryLoad(_filePath, out data);
}
```

### Custom Save Handlers
```csharp
using SaveEncryptionToolkit.Handlers;

public sealed class CustomSaveHandler : ISaveHandler
{
    public string Serialize<T>(T data) => /* custom serializer */;
    public T Deserialize<T>(string data) => /* custom deserializer */;
}
```

## Troubleshooting

- **Legacy / Unencoded Settings**: Run **Tools > Save Encryption Toolkit > Fix Unencoded Encryption Settings** to re-encode any unencoded `EncryptionSettings` assets.
- **Security Note**: Do not commit `EncryptionSettings.asset` to public repositories or bundle it in player builds. Use the generated `ProductionKeyStore` for builds.
- **`BinarySaveHandler`**: Uses `BinaryFormatter`, which is obsolete in modern .NET. Prefer `JsonSaveHandler` or a custom `ISaveHandler` for new projects.

## License
[MIT License](LICENSE)
