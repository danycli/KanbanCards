// Portions derived from Filee (MIT License - Copyright (c) KnifeLemon).
// Adapted for KanbanCards WPF architecture and Authenticode verification.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KanbanCards.Services;

/// <summary>
/// Authenticode verification helper for checking digital signatures of executables.
/// </summary>
public static class Authenticode
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
    private const IntPtr InvalidWindow = -1;

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_REVOKE_WHOLECHAIN = 1;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, [In] ref WINTRUST_DATA pWVTData);

    /// <summary>
    /// Returns the subject of the certificate that signed <paramref name="path"/>, or null if the file
    /// is unsigned, untrusted, or if signature verification fails.
    /// </summary>
    public static string? TrustedSigner(string path, bool checkRevocation = false)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var action = GenericVerifyV2;

            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = fullPath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            var pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            try
            {
                Marshal.StructureToPtr(fileInfo, pFileInfo, false);

                var data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WTD_UI_NONE,
                    fdwRevocationChecks = checkRevocation ? WTD_REVOKE_WHOLECHAIN : WTD_REVOKE_NONE,
                    dwUnionChoice = WTD_CHOICE_FILE,
                    pFile = pFileInfo,
                    dwStateAction = WTD_STATEACTION_VERIFY,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = IntPtr.Zero,
                    dwProvFlags = checkRevocation ? WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT : 0,
                    dwUIContext = 0,
                    pSignatureSettings = IntPtr.Zero
                };

                try
                {
                    var result = WinVerifyTrust(InvalidWindow, ref action, ref data);
                    if (result != 0)
                        return null;

                    // Extract signer certificate subject
                    using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(fullPath));
                    return cert.Subject;
                }
                finally
                {
                    data.dwStateAction = WTD_STATEACTION_CLOSE;
                    WinVerifyTrust(InvalidWindow, ref action, ref data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFileInfo);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or CryptographicException or IOException)
        {
            return null;
        }
    }
}
