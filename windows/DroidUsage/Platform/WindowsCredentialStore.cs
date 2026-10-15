using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using DroidUsage.Services.Abstractions;

namespace DroidUsage.Platform;

public sealed class WindowsCredentialStore : ISecretStore
{
    private const uint CRED_TYPE_GENERIC = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);

    public string? GetGenericPassword(string targetName)
    {
        if (!CredRead(targetName, CRED_TYPE_GENERIC, 0, out IntPtr credPtr) || credPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
        }
        finally
        {
            CredFree(credPtr);
        }
    }
}
