using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PanBridge;

public static class SessionVault
{
    // Preserve the encryption context used by existing installations before the rename.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JboxDrive/session/v1");
    public static void Save(AuthSession session, string? path = null)
    {
        if (!SessionRules.ValidToken(session.UserToken) || session.Cookies.Any(c => !SessionRules.AllowedDomain(c.Domain)))
            throw new InvalidDataException("Invalid session.");
        path ??= AppPaths.Vault;
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(session);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static AuthSession? Load(string? path = null)
    {
        path ??= AppPaths.Vault;
        if (!File.Exists(path)) return null;
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            var session = JsonSerializer.Deserialize<AuthSession>(plain);
            if (session is null || !SessionRules.ValidToken(session.UserToken) || session.Cookies.Any(c => !SessionRules.AllowedDomain(c.Domain))) throw new InvalidDataException("Invalid session.");
            return session;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Clear() { if (File.Exists(AppPaths.Vault)) File.Delete(AppPaths.Vault); }
}
