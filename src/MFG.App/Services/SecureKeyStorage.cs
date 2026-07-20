using System.Security.Cryptography;
using System.Text;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MFG.App.Data;

namespace MFG.App.Services;

public sealed class SecureKeyStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MoonlightForwarderGenerator:prod.keys:v1");
    private static readonly string StorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator",
        "secrets");
    private static readonly string StoragePath = Path.Combine(StorageDirectory, "prod.keys.protected");

    public bool IsConfigured => File.Exists(StoragePath);

    public async Task<byte[]> ReadAsync()
    {
        if (!File.Exists(StoragePath))
            throw new InvalidOperationException("Conclua a configuracao do Switch antes de criar atalhos.");

        var protectedKey = await File.ReadAllBytesAsync(StoragePath);
        try
        {
            return ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedKey);
        }
    }

    public async Task ImportAsync(string sourcePath)
    {
        var key = await File.ReadAllBytesAsync(sourcePath);
        try
        {
            if (key.Length == 0 || key.Length > 1024 * 1024)
                throw new InvalidDataException("O arquivo selecionado não parece ser uma chave válida.");

            var protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                var temporaryPath = $"{StoragePath}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllBytesAsync(temporaryPath, protectedKey);
                File.Move(temporaryPath, StoragePath, true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
            }

            var hash = Convert.ToHexString(SHA256.HashData(key));
            await using var database = MfgDbContext.Create();
            var setting = await database.SecureKeySettings.FindAsync(1)
                ?? new SecureKeySetting { Id = 1 };
            if (database.Entry(setting).State == EntityState.Detached)
                database.SecureKeySettings.Add(setting);

            setting.Sha256 = hash;
            setting.ImportedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task RemoveAsync()
    {
        if (File.Exists(StoragePath))
            File.Delete(StoragePath);

        await using var database = MfgDbContext.Create();
        var setting = await database.SecureKeySettings.FindAsync(1);
        if (setting is not null)
        {
            database.SecureKeySettings.Remove(setting);
            await database.SaveChangesAsync();
        }
    }
}
