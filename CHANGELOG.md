# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-06-14
### Added
- Authenticated Encrypt-then-MAC APIs on `EncryptionUtility` (`EncryptAuthenticated`, `DecryptAuthenticated`, `TryDecryptAuthenticated`, `EncryptBytes`, `DecryptBytes`) using a fresh CSPRNG 128-bit IV per save and constant-time HMAC-SHA256 verification (`SET1` envelope).
- `EncryptedSaveStore` runtime file store combining `ISaveHandler` + `EncryptionUtility` with atomic temp-file writes and automatic `.bak` fallback recovery on primary file corruption or tampering.
- `EncryptionUtility.IsInitialized`, `EncryptionUtility.Reset()`, `EncryptionUtility.TryDecrypt`, and `byte[]` `Initialize` overload.
- `KeyObfuscator.TryDecode` / `KeyObfuscator.IsEncoded` and `EncryptionSettings.HasValidKeys()`.
- Key status indicator and **Verify Keys (Round-Trip Test)** button in `EncryptionSettingsEditor`.
- EditMode NUnit test suite covering key generation, obfuscation tamper detection, legacy and authenticated AES encryption, and atomic backup recovery.

### Changed
- `EncryptionFixer` now validates encoded keys via `KeyObfuscator.IsEncoded` instead of checking only for the `::` delimiter.

## [1.0.0] - 2025-06-26
### Added
- Initial release with `EncryptionUtility`, `KeyGenerator`, `KeyObfuscator`, `EncryptionSettings`, `JsonSaveHandler`, `BinarySaveHandler`, `EncryptionSettingsEditor`, `EncryptionFixer`, and `Samples~/EncryptedSaveManager`.
