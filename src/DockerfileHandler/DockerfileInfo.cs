using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Helium.DockerfileHandler.Commands;
using Helium.Util;

namespace Helium.DockerfileHandler
{
    public class DockerfileInfo
    {

        public DockerfileInfo(IEnumerable<string> unconsumedBuildArgs, IEnumerable<DockerfileBuild> commands) {
            UnconsumedBuildArgs = new ReadOnlyCollectionNoList<string>(new HashSet<string>(unconsumedBuildArgs));
            Commands = commands.ToList().AsReadOnly();
        }

        public IReadOnlyCollection<string> UnconsumedBuildArgs { get; }
        
        public IReadOnlyList<DockerfileBuild> Commands { get; }
    }
}