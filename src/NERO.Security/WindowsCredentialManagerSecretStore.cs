using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NERO.Security;

/// <summary>Stores generic credentials in Windows Credential Manager for the current user.</summary>
public sealed partial class WindowsCredentialManagerSecretStore : IDeviceSecretStore
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public void Save(string targetName, string secret)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var target = Marshal.StringToHGlobalUni(targetName);
        var userName = Marshal.StringToHGlobalUni("NERO");
        var blob = Marshal.StringToHGlobalUni(secret);
        try
        {
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = target,
                CredentialBlobSize = checked((uint)((secret.Length + 1) * sizeof(char))),
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = userName
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(),
                    "Windows Credential Manager could not save the NERO credential.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
            Marshal.FreeHGlobal(userName);
            Marshal.FreeHGlobal(target);
        }
    }

    public string? Read(string targetName)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!NativeMethods.CredRead(targetName, GenericCredential, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorNotFound) return null;
            throw new Win32Exception(error, "Windows Credential Manager could not read the NERO credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            return credential.CredentialBlob == IntPtr.Zero
                ? null
                : Marshal.PtrToStringUni(credential.CredentialBlob);
        }
        finally
        {
            NativeMethods.CredFree(credentialPointer);
        }
    }

    public bool Delete(string targetName)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (NativeMethods.CredDelete(targetName, GenericCredential, 0)) return true;
        var error = Marshal.GetLastPInvokeError();
        if (error == ErrorNotFound) return false;
        throw new Win32Exception(error, "Windows Credential Manager could not delete the NERO credential.");
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NERO device credentials require Windows Credential Manager.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("Advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CredWrite(ref NativeCredential credential, uint flags);

        [LibraryImport("Advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CredRead(string targetName, uint type, uint flags, out IntPtr credentialPointer);

        [LibraryImport("Advapi32.dll", EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CredDelete(string targetName, uint type, uint flags);

        [LibraryImport("Kernel32.dll", EntryPoint = "CredFree")]
        internal static partial void CredFree(IntPtr buffer);
    }
}
