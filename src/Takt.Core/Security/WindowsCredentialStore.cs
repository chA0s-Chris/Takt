// Copyright (c) 2026 Christian Flessa. All rights reserved.
// This file is licensed under the MIT license. See LICENSE in the project root for more information.
namespace Takt.Core.Security;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

/// <summary>
/// Credential store backed by the Windows Credential Manager via <c>advapi32</c>.
/// Entries are generic credentials named <c>Takt/&lt;name&gt;</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const UInt32 CredPersistLocalMachine = 2;
    private const UInt32 CredTypeGeneric = 1;
    private const Int32 ErrorNotFound = 1168;
    private const String TargetPrefix = "Takt/";

    /// <summary>Creates the store.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    public WindowsCredentialStore()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows Credential Manager is only available on Windows.");
        }
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Boolean CredDeleteW(String target, UInt32 type, UInt32 flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Boolean CredReadW(String target, UInt32 type, UInt32 flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Boolean CredWriteW(ref NativeCredential credential, UInt32 flags);

    /// <inheritdoc/>
    public String? Get(String name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!CredReadW(TargetPrefix + name, CredTypeGeneric, 0, out var credentialPointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return String.Empty;
            }

            var blob = new Byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            return Encoding.UTF8.GetString(blob);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    /// <inheritdoc/>
    public void Remove(String name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!CredDeleteW(TargetPrefix + name, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error);
            }
        }
    }

    /// <inheritdoc/>
    public void Set(String name, String value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var blob = Encoding.UTF8.GetBytes(value);
        var targetPointer = Marshal.StringToCoTaskMemUni(TargetPrefix + name);
        var blobPointer = Marshal.AllocCoTaskMem(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPointer, blob.Length);
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetPointer,
                CredentialBlobSize = (UInt32)blob.Length,
                CredentialBlob = blobPointer,
                Persist = CredPersistLocalMachine
            };
            if (!CredWriteW(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blobPointer);
            Marshal.FreeCoTaskMem(targetPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public UInt32 Flags;
        public UInt32 Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public UInt32 CredentialBlobSize;
        public IntPtr CredentialBlob;
        public UInt32 Persist;
        public UInt32 AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
