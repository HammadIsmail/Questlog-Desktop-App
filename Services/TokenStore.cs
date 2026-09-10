using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Questlog.Services;

/// <summary>
/// Persists the JWT access token and user info to Windows Credential Manager
/// so the user stays logged in across app restarts.
/// </summary>
public static class TokenStore
{
    private const string CredentialTarget = "Questlog_Auth_Token";

    // ── Win32 Credential Manager P/Invoke ──────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;               // CRED_TYPE_GENERIC = 1
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;            // CRED_PERSIST_LOCAL_MACHINE = 2
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    // ── Stored payload ─────────────────────────────────────────────────────

    private record StoredAuth(string Token, string Email, string Name, DateTimeOffset SavedAt);

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Save token + user info to Windows Credential Manager.</summary>
    public static void Save(string token, string email, string name)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new StoredAuth(token, email, name, DateTimeOffset.UtcNow));
            var blobBytes = Encoding.UTF8.GetBytes(payload);

            var handle = Marshal.AllocHGlobal(blobBytes.Length);
            try
            {
                Marshal.Copy(blobBytes, 0, handle, blobBytes.Length);

                var cred = new CREDENTIAL
                {
                    Type = 1,           // CRED_TYPE_GENERIC
                    TargetName = CredentialTarget,
                    Comment = "Questlog JWT session",
                    CredentialBlobSize = blobBytes.Length,
                    CredentialBlob = handle,
                    Persist = 2,        // CRED_PERSIST_LOCAL_MACHINE
                    UserName = email
                };

                CredWrite(ref cred, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }
        }
        catch
        {
            // Non-critical — swallow silently
        }
    }

    /// <summary>
    /// Try to read a previously saved token.
    /// Returns null if nothing is saved or the token appears expired (> 24h old).
    /// </summary>
    public static (string Token, string Email, string Name)? TryLoad()
    {
        try
        {
            if (!CredRead(CredentialTarget, 1, 0, out var ptr)) return null;

            try
            {
                var raw = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                var blobBytes = new byte[raw.CredentialBlobSize];
                Marshal.Copy(raw.CredentialBlob, blobBytes, 0, raw.CredentialBlobSize);
                var json = Encoding.UTF8.GetString(blobBytes);

                var stored = JsonSerializer.Deserialize<StoredAuth>(json);
                if (stored == null) return null;

                // Treat tokens older than 23 hours as stale (backend JWT is 24h)
                if (DateTimeOffset.UtcNow - stored.SavedAt > TimeSpan.FromHours(23))
                {
                    Clear();
                    return null;
                }

                return (stored.Token, stored.Email, stored.Name);
            }
            finally
            {
                CredFree(ptr);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Delete the saved credential (called on explicit logout).</summary>
    public static void Clear()
    {
        try { CredDelete(CredentialTarget, 1, 0); }
        catch { /* ignore */ }
    }
}
