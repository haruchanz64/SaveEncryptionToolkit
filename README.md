# Save Encryption Toolkit

Encryption-ready save utilities for Unity 6 that separate AES encryption, HMAC integrity checks, atomic file persistence, and backup recovery from your save schemas.

## Features

- **Authenticated Encrypt-then-MAC encryption (`EncryptionUtility`):** `EncryptAuthenticated` / `DecryptAuthenticated`, `TryDecryptAuthenticated`, and binary `EncryptBytes` / `DecryptBytes` wrap payloads in a versioned `SET1` envelope (`[SET1 (4B)][Random 128-bit IV (16B)][AES-CBC Ciphertext][HMAC-SHA256 (32B)]`). Each write generates a fresh CSPRNG IV, derives a domain-separated HMAC key via SHA-256, and verifies the tag in constant time before decrypting.
- **Legacy static-IV compatibility (`EncryptionUtility`):** Retains `Encrypt`, `Decrypt`, and `TryDecrypt` using the configured static 128-bit IV so existing save files stay readable while new saves adopt authenticated envelopes.
- **Atomic file storage and backup recovery (`EncryptedSaveStore`):** Stages writes to a `.tmp` file, rotates the prior save to `.bak`, and replaces the primary file. On read failure (corrupted bytes, HMAC mismatch, or malformed payload), `Load` and `TryLoad` automatically fall back to `.bak` when backup maintenance is enabled.
- **Pluggable serialization (`ISaveHandler`):** Decouples object serialization from encryption and file I/O. Ships with `JsonSaveHandler` (backed by Unity `JsonUtility`, recommended) and legacy `BinarySaveHandler` (backed by `BinaryFormatter` + Base64).
- **Key generation, obfuscation, and editor workflow (`KeyGenerator`, `KeyObfuscator`, `EncryptionSettings`):** Generates 256-bit AES keys and 128-bit IVs, encodes them in `salt::base64` format with trailing-salt verification, emits `ProductionKeyStore.cs` from the `EncryptionSettings` Inspector (`EncryptionSettingsEditor`), runs round-trip verification tests, and repairs unencoded settings assets via `EncryptionFixer`.

## Requirements

- Unity `6000.0` or newer
- Zero required external dependencies

## Installation

### Unity Package Manager (Git URL)

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) and choose **Install package from git URL...**.
3. Enter:

```text
https://github.com/haruchanz64/SaveEncryptionToolkit.git
```

### Local package

Add the package path to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.haruchanz64.saveencryptiontoolkit": "file:../Plugins/SaveEncryptionToolkit"
  }
}
```

## Quick start

1. Create a settings asset via **Assets > Create > Save Encryption Toolkit > Encryption Settings**.
2. Select the asset in the Project window and click **Generate & Apply Secure Save Keys** in the Inspector. This generates a 256-bit AES key and 128-bit IV, stores them obfuscated in the asset, and emits `ProductionKeyStore.cs` under `Assets/SaveEncryptionToolkit/Runtime` (or `<toolkitRoot>/Runtime` when the toolkit is installed directly under `Assets/`).
3. Click **Verify Keys (Round-Trip Test)** in the Inspector to confirm the key pair passes an authenticated AES + HMAC-SHA256 round-trip probe.
4. During runtime startup, initialize `EncryptionUtility` with `ProductionKeyStore.GetDecryptedKey()` and `ProductionKeyStore.GetDecryptedIv()` before calling `EncryptedSaveStore` or `EncryptionUtility` encryption methods.
5. Keep `EncryptionSettings.asset` out of public repositories and player builds; ship `ProductionKeyStore.cs` in builds instead.

## Core concepts

### Authenticated `SET1` envelope vs. legacy AES-CBC

`EncryptionUtility` supports two payload formats using the key material supplied to `Initialize(...)`:

- **Authenticated (`SET1`) format:** Used by `EncryptAuthenticated`, `DecryptAuthenticated`, `TryDecryptAuthenticated`, `EncryptBytes`, and `DecryptBytes`. Every write generates a fresh random 16-byte IV and builds `[SET1 (4B)][IV (16B)][AES-CBC Ciphertext][HMAC-SHA256 (32B)]`. The HMAC key is derived once during `Initialize` as `SHA-256(key || "SET1-HMAC-SHA256")` so encryption and authentication do not reuse the raw AES key. Decryption verifies the magic header and compares the MAC in constant time before running AES decryption, throwing `CryptographicException` on truncation or tampering.
- **Legacy static-IV format:** Used by `Encrypt`, `Decrypt`, and `TryDecrypt`. Encrypts UTF-8 strings using AES-CBC with the static 16-byte IV passed to `Initialize` and no MAC tag. Retained only for reading existing saves.

`EncryptionUtility` clears and zeroes its static key, IV, and MAC key buffers automatically on `RuntimeInitializeLoadType.SubsystemRegistration` (between Play Mode sessions) and whenever `EncryptionUtility.Reset()` is called.

### Atomic writes and `.bak` recovery

`EncryptedSaveStore` combines an `ISaveHandler` with `EncryptionUtility` for disk persistence:

1. `Save<T>(filePath, data)` creates missing parent directories, serializes `data`, encrypts the string, and writes the payload to `<filePath>.tmp`.
2. If `<filePath>` already exists and `maintainBackup` is `true`, the existing file is copied to `<filePath>.bak`, the old primary file is removed, and `.tmp` is moved into `<filePath>`.
3. `Load<T>(filePath)` and `TryLoad<T>(filePath, out data)` try the primary file first and fall back to `<filePath>.bak` automatically if the primary file is missing, truncated, fails HMAC verification, or fails deserialization.
4. `Exists(filePath)` returns `true` when either the primary file or its `.bak` backup (when enabled) exists. `Delete(filePath, deleteBackup)` removes the primary file, any leftover `.tmp` file, and optionally `.bak`.

### Key obfuscation and `ProductionKeyStore`

`KeyObfuscator.Encode` appends a random alphanumeric salt (12 characters by default), Base64-encodes the combined UTF-8 string, and stores it as `salt::base64`. `KeyObfuscator.Decode` splits on `::` and verifies that the decoded string ends with the prefix salt, throwing on malformed delimiters or modified salts. `EncryptionSettingsEditor` writes these obfuscated literals into a generated `SaveEncryptionToolkit.Runtime.ProductionKeyStore` static class so runtime builds do not need to bundle `EncryptionSettings.asset`.

## Code examples

### 1. Save and load encrypted data with `EncryptedSaveStore`

Initialize `EncryptionUtility` once on startup using the generated `ProductionKeyStore`, then use `EncryptedSaveStore` for authenticated saves with `.bak` recovery:

```csharp
using System;
using System.IO;
using SaveEncryptionToolkit.Encryption;
using SaveEncryptionToolkit.Handlers;
using SaveEncryptionToolkit.Runtime;
using SaveEncryptionToolkit.Storage;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public int level;
    public float health;
    public string[] inventory;
}

public sealed class GameSaveSystem : MonoBehaviour
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

    public void Save(PlayerSaveData data) => _store.Save(_filePath, data);

    public bool TryLoad(out PlayerSaveData data) => _store.TryLoad(_filePath, out data);

    public void DeleteSave() => _store.Delete(_filePath, deleteBackup: true);
}
```

### 2. Implement a custom `ISaveHandler`

Implement `ISaveHandler` to plug in another serializer (for example, `Newtonsoft.Json` or a custom string-encoded format) for types Unity's `JsonUtility` does not support directly:

```csharp
using System;
using SaveEncryptionToolkit.Handlers;
using SaveEncryptionToolkit.Storage;

public sealed class CustomJsonSaveHandler : ISaveHandler
{
    public string Serialize<T>(T data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Replace with your serializer of choice (e.g., JsonConvert.SerializeObject(data)).
        return UnityEngine.JsonUtility.ToJson(data);
    }

    public T Deserialize<T>(string data)
    {
        if (string.IsNullOrEmpty(data))
            throw new ArgumentException("Serialized data cannot be null or empty.", nameof(data));

        // Replace with your deserializer of choice (e.g., JsonConvert.DeserializeObject<T>(data)).
        return UnityEngine.JsonUtility.FromJson<T>(data);
    }
}

public static class CustomStoreFactory
{
    public static EncryptedSaveStore Create()
    {
        return new EncryptedSaveStore(
            handler: new CustomJsonSaveHandler(),
            useAuthenticatedEncryption: true,
            maintainBackup: true);
    }
}
```

### 3. Encrypt raw byte buffers directly

Use `EncryptionUtility.EncryptBytes` and `EncryptionUtility.DecryptBytes` when saving binary formats such as screenshots, compressed snapshots, or custom binary streams:

```csharp
using System.IO;
using System.Security.Cryptography;
using SaveEncryptionToolkit.Encryption;
using UnityEngine;

public static class BinaryBlobSaver
{
    public static void SaveBytes(string path, byte[] rawData)
    {
        byte[] envelope = EncryptionUtility.EncryptBytes(rawData);
        File.WriteAllBytes(path, envelope);
    }

    public static bool TryLoadBytes(string path, out byte[] rawData)
    {
        rawData = null;
        if (!File.Exists(path))
            return false;

        try
        {
            byte[] envelope = File.ReadAllBytes(path);
            rawData = EncryptionUtility.DecryptBytes(envelope);
            return true;
        }
        catch (CryptographicException ex)
        {
            Debug.LogWarning($"Encrypted blob failed verification: {ex.Message}");
            return false;
        }
    }
}
```

## Extension contracts and core API

| Type | Namespace | Purpose | Built-in implementation |
| :--- | :--- | :--- | :--- |
| `ISaveHandler` | `SaveEncryptionToolkit.Handlers` | Converts save objects to and from strings around encryption | `JsonSaveHandler`, `BinarySaveHandler` |
| `EncryptedSaveStore` | `SaveEncryptionToolkit.Storage` | High-level file persistence with `.tmp` staging and `.bak` recovery | `EncryptedSaveStore` |
| `EncryptionUtility` | `SaveEncryptionToolkit.Encryption` | Static AES-CBC + HMAC-SHA256 (`SET1`) and legacy static-IV encryption API | `EncryptionUtility` |
| `EncryptionSettings` | `SaveEncryptionToolkit.Encryption` | ScriptableObject holding obfuscated key and IV strings for editor setup | `EncryptionSettings` |
| `KeyGenerator` | `SaveEncryptionToolkit.Encryption` | Generates random 256-bit AES keys and 128-bit IVs as Base64 strings | `KeyGenerator` |
| `KeyObfuscator` | `SaveEncryptionToolkit.Encryption` | Salted Base64 obfuscation (`salt::base64`) with salt-suffix tamper check | `KeyObfuscator` |
| `ProductionKeyStore` | `SaveEncryptionToolkit.Runtime` | Generated static class embedding obfuscated key and IV for builds | Generated by `EncryptionSettingsEditor` |
| `EncryptionSettingsEditor` | `SaveEncryptionToolkit.Editor` | Custom Inspector for key generation, `ProductionKeyStore.cs` emission, and round-trip testing | `EncryptionSettingsEditor` |
| `EncryptionFixer` | `SaveEncryptionToolkit.Editor` | Menu utility that re-encodes unencoded `EncryptionSettings` assets | `EncryptionFixer` |

## Included samples

Import **Encrypted Save Manager Sample** (`Samples~/EncryptedSaveManager`) from the Package Manager **Samples** tab:

- `SampleEncryptedSaveManager` (with `SamplePlayerSaveData` in `SaveEncryptionToolkit.Samples`): example `MonoBehaviour` wiring an `EncryptionSettings` asset, `EncryptionUtility`, and `JsonSaveHandler` to write and read `player_save.enc` under `Application.persistentDataPath` using Inspector context menu actions (`Save Encrypted Data` and `Load Encrypted Data`). Note that this minimal sample calls legacy `EncryptionUtility.Encrypt` / `Decrypt`; use `EncryptedSaveStore` or `EncryptAuthenticated` / `DecryptAuthenticated` for new production saves.

## Testing

The package includes EditMode NUnit tests under `Tests/Editor/` runnable from **Window > General > Test Runner** (EditMode tab):

- `Tests/Editor/EncryptionUtilityAndStoreTests.cs`: verifies legacy AES round-trip, authenticated `SET1` random IV uniqueness and single-bit tamper rejection (`CryptographicException` and `TryDecryptAuthenticated`), `EncryptedSaveStore` save/load/`.bak` rotation/corruption recovery/delete lifecycle, and `EncryptionUtility.Reset()` state cleanup.
- `Tests/Editor/KeyObfuscatorAndGeneratorTests.cs`: verifies `KeyGenerator.GenerateKeyAndIv()` produces distinct 32-byte AES-256 keys and 16-byte IVs, and verifies `KeyObfuscator` encode/decode, `IsEncoded`, salt tamper detection, and malformed delimiter handling.

## Troubleshooting and limitations

- **Unencoded or legacy `EncryptionSettings` assets:** Run **Tools > HaruChanZ64 > Assets > Save Encryption Toolkit > Fix Unencoded Encryption Settings** to scan all `EncryptionSettings` assets in the project and re-encode any non-empty key or IV values that fail `KeyObfuscator.IsEncoded`.
- **`InvalidOperationException: EncryptionUtility is not initialized`:** `EncryptionUtility` clears static key buffers on every domain reload (`RuntimeInitializeLoadType.SubsystemRegistration`) and when `Reset()` is called. Always call `EncryptionUtility.Initialize(...)` during game startup before saving or loading.
- **`ProductionKeyStore.cs` output path in UPM installs:** Because `Packages/` is read-only for UPM packages, `EncryptionSettingsEditor` writes `ProductionKeyStore.cs` to `Assets/SaveEncryptionToolkit/Runtime` unless the toolkit itself is placed under `Assets/`.
- **Client-side obfuscation is not cryptographic secrecy:** `KeyObfuscator` and `ProductionKeyStore` keep raw Base64 keys out of plain text and deter casual save editing, but any key embedded in a shipped client binary can be extracted through reverse engineering. Do not rely on client-side encryption for server-authoritative anti-cheat or sensitive secrets.
- **`JsonSaveHandler` serialization rules:** Backed by Unity `JsonUtility`, which requires `[Serializable]` classes or structs with Unity-serializable fields. Top-level dictionaries, polymorphic lists, or properties without backing fields require a custom `ISaveHandler`.
- **`BinarySaveHandler` deprecation:** Uses `BinaryFormatter`, which is obsolete in modern .NET (`SYSLIB0011`) and unsafe on untrusted input. Use it only when migrating legacy saves; prefer `JsonSaveHandler` or a custom `ISaveHandler` for new projects.
- **Write interruption between delete and rename:** `EncryptedSaveStore.Save` writes `.tmp`, copies the existing save to `.bak`, deletes the primary file, and moves `.tmp` to the primary path. If a crash interrupts execution between deleting the primary file and moving `.tmp`, the prior valid save remains in `.bak` and `Load` / `TryLoad` recovers from it automatically when `maintainBackup` is enabled.

## License

[MIT License](LICENSE)
