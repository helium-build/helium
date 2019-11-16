using System;
using System.Threading.Tasks;

namespace Helium.Engine.Record
{
    internal class ReplayRecorder : IRecorder
    {
        public ReplayRecorder(string archiveFile, string workDir) {
            throw new NotImplementedException();
        }

        public static Task<IRecorder> Create(string archiveFile, string workDir) {
            throw new NotImplementedException();
        }
    }
}