using System;
using System.IO;
using System.Runtime.InteropServices;

namespace QuanLyGiuXe.Services
{
    internal static class NativeLibraryLoader
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// Ensure a native SDK DLL is loaded from an architecture-specific folder under the app base directory
        /// or from an external SDK path specified by ZKTECO_PULLSDK_PATH environment variable.
        /// Expected layout: <base>\Libs\C3SDK\x64\plcommpro.dll (for x64) or ...\x86\ (for x86)
        /// Falls back to <base>\Libs\C3SDK\plcommpro.dll if arch-specific not found.
        /// Returns true when the DLL was found and LoadLibrary succeeded.
        /// </summary>
        public static bool EnsureSdkLoaded(string dllName = "plcommpro.dll")
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

                string archFolder = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "x64" :
                                    RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";

                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;

                // Check external SDK path first (useful during development)
                var envPath = Environment.GetEnvironmentVariable("ZKTECO_PULLSDK_PATH");
                if (!string.IsNullOrWhiteSpace(envPath))
                {
                    var candidate = Path.Combine(envPath, archFolder, dllName);
                    if (File.Exists(candidate))
                    {
                        var h = LoadLibrary(candidate);
                        return h != IntPtr.Zero;
                    }

                    candidate = Path.Combine(envPath, dllName);
                    if (File.Exists(candidate))
                    {
                        var h = LoadLibrary(candidate);
                        return h != IntPtr.Zero;
                    }
                }

                string candidateLocal = Path.Combine(baseDir, "Libs", "C3SDK", archFolder, dllName);
                if (!File.Exists(candidateLocal))
                {
                    // fallback: root of Libs\C3SDK
                    candidateLocal = Path.Combine(baseDir, "Libs", "C3SDK", dllName);
                }

                if (!File.Exists(candidateLocal)) return false;

                var handle = LoadLibrary(candidateLocal);
                return handle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
    }
}
