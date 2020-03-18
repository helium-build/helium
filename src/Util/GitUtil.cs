using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class GitUtil
    {
        public static async Task CloneRepo(string url, string dir, string branch, int? depth = null) {
            await RunGitCommand(
                "clone",
                (depth is int d ? "--depth=" + d : null),
                (branch != null ? "--branch" : null),
                branch,
                "--",
                url,
                dir
            );
        }

        private static async Task RunGitCommand(params string?[] args) {
            var psi = new ProcessStartInfo {
                FileName = "git",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            
            foreach(var arg in args) {
                if(arg != null) {
                    psi.ArgumentList.Add(arg);
                }
            }

            var process = Process.Start(psi);

            await process.WaitForExitAsync();

            if(process.ExitCode != 0) {
                throw new Exception($"Git command failed with exit code {process.ExitCode}");
            }
        }
    }
}