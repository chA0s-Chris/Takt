// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Security;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Credential store backed by an AES-GCM-encrypted file, used where no system
/// credential manager is available (Linux). The random key lives in a sibling file;
/// both files are restricted to the owning user. This is deliberately the weaker
/// store: it protects against casual reads and backups of the data directory, not
/// against an attacker running as the same user.
/// </summary>
public sealed class EncryptedFileCredentialStore : ICredentialStore
{
    private const Int32 KeySizeInBytes = 32;
    private const Int32 NonceSizeInBytes = 12;
    private const Int32 TagSizeInBytes = 16;

    private readonly String _keyPath;
    private readonly String _storePath;

    /// <summary>Creates the store inside the given directory.</summary>
    /// <param name="directory">The directory the key and store files live in; created if missing.</param>
    public EncryptedFileCredentialStore(String directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        _keyPath = Path.Combine(directory, "credentials.key");
        _storePath = Path.Combine(directory, "credentials.dat");
    }

    private static void RestrictToOwner(String path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private String Decrypt(String payload)
    {
        var data = Convert.FromBase64String(payload);
        var nonce = data.AsSpan(0, NonceSizeInBytes);
        var tag = data.AsSpan(NonceSizeInBytes, TagSizeInBytes);
        var ciphertext = data.AsSpan(NonceSizeInBytes + TagSizeInBytes);
        var plaintext = new Byte[ciphertext.Length];
        using var aes = new AesGcm(GetOrCreateKey(), TagSizeInBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private String Encrypt(String value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeInBytes);
        var tag = new Byte[TagSizeInBytes];
        var ciphertext = new Byte[plaintext.Length];
        using var aes = new AesGcm(GetOrCreateKey(), TagSizeInBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new Byte[NonceSizeInBytes + TagSizeInBytes + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSizeInBytes);
        ciphertext.CopyTo(payload, NonceSizeInBytes + TagSizeInBytes);
        return Convert.ToBase64String(payload);
    }

    private Byte[] GetOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            return File.ReadAllBytes(_keyPath);
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeInBytes);
        File.WriteAllBytes(_keyPath, key);
        RestrictToOwner(_keyPath);
        return key;
    }

    private Dictionary<String, String> LoadEntries()
    {
        if (!File.Exists(_storePath))
        {
            return new(StringComparer.Ordinal);
        }

        var json = File.ReadAllText(_storePath);
        return JsonSerializer.Deserialize<Dictionary<String, String>>(json)
               ?? new Dictionary<String, String>(StringComparer.Ordinal);
    }

    private void SaveEntries(Dictionary<String, String> entries)
    {
        File.WriteAllText(_storePath, JsonSerializer.Serialize(entries));
        RestrictToOwner(_storePath);
    }

    /// <inheritdoc/>
    public String? Get(String name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var entries = LoadEntries();
        return entries.TryGetValue(name, out var payload) ? Decrypt(payload) : null;
    }

    /// <inheritdoc/>
    public void Remove(String name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var entries = LoadEntries();
        if (entries.Remove(name))
        {
            SaveEntries(entries);
        }
    }

    /// <inheritdoc/>
    public void Set(String name, String value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        var entries = LoadEntries();
        entries[name] = Encrypt(value);
        SaveEntries(entries);
    }
}
