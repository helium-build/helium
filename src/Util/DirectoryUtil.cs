using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Mono.Unix;
using Mono.Unix.Native;

namespace Helium.Util
{
    public static class DirectoryUtil
    {
        public static bool CreateNewDirectory(string path) {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                if(CreateDirectory(path, IntPtr.Zero)) {
                    return true;
                }

                var error = Marshal.GetLastWin32Error(); 
                if(error == ERROR_ALREADY_EXISTS) {
                    return false;
                }
                
                throw new Win32Exception(error);
            }
            else {
                if(Syscall.mkdir(path, FilePermissions.ACCESSPERMS) == 0) {
                    return true;
                }

                if(Stdlib.GetLastError() != Errno.EEXIST) {
                    UnixMarshal.ThrowExceptionForLastError();
                }
            
                return false;
            }
        }

        public static string CreateTempDirectory(string parent, string prefix = "") {
            string path;
            do {
                path = Path.Combine(parent, prefix + Path.GetRandomFileName());
            } while(!CreateNewDirectory(path));

            return path;
        }
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);
        
        private const int ERROR_ALREADY_EXISTS = 183;

    }
}