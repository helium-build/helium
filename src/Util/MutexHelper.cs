using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;

namespace Helium.Util
{
    public static class MutexHelper
    {
        public static void Lock(string lockFile, Action f, CancellationToken cancellationToken) =>
            Lock<object?>(lockFile, () => {
                f();
                return null;
            }, cancellationToken);

        public static T Lock<T>(string lockFile, Func<T> f, CancellationToken cancellationToken) {
            using var fileStream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return LockFileWindows(fileStream, f, cancellationToken);
            }
            else {
                return LockFileUnix(fileStream, f, cancellationToken);
            }
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool LockFileEx(IntPtr hFile, uint dwFlags, uint dwReserved, uint nNumberOfBytesToLockLow,
            uint nNumberOfBytesToLockHigh, [In] ref NativeOverlapped lpOverlapped);
        
        private const uint LOCKFILE_EXCLUSIVE_LOCK = 0x00000002;
        
        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool UnlockFileEx(IntPtr hFile, uint dwReserved, uint nNumberOfBytesToLockLow,
            uint nNumberOfBytesToLockHigh, [In] ref NativeOverlapped lpOverlapped);
        
        private static T LockFileWindows<T>(FileStream fileStream, Func<T> f, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = fileStream.SafeFileHandle.DangerousGetHandle();


            NativeOverlapped overlapped = default;
            if(!LockFileEx(handle, LOCKFILE_EXCLUSIVE_LOCK, 0, 1, 0, ref overlapped)) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
                
            try {
                return f();
            }
            finally {
                overlapped = default;
                if(!UnlockFileEx(handle, 0, 1, 0, ref overlapped)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        
        private static T LockFileUnix<T>(FileStream fileStream, Func<T> f, CancellationToken cancellationToken) {
            while(true) {
                cancellationToken.ThrowIfCancellationRequested();
                int fd = (int)fileStream.SafeFileHandle.DangerousGetHandle();

                var flock = new Flock {
                    l_type = LockType.F_WRLCK,
                    l_whence = SeekFlags.SEEK_SET,
                    l_start = 0,
                    l_len = 0,
                };
                if(Syscall.fcntl(fd, FcntlCommand.F_SETLKW, ref flock) < 0) {
                    switch(Stdlib.GetLastError()) {
                        case Errno.EACCES:
                        case Errno.EAGAIN:
                            continue;
                            
                        case var error:
                            throw new UnixIOException(error);
                    }
                }
                
                try {
                    return f();
                }
                finally {
                    flock.l_type = LockType.F_UNLCK;
                    Syscall.fcntl(fd, FcntlCommand.F_SETLK, ref flock);
                }
            }
        }

    }
}