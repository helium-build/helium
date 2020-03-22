using System.Threading.Tasks;

namespace ContainerBuildProxy
{
    public class SavedFileInfo
    {
        public SavedFileInfo(bool useHeadResponse, string outputFile, TaskCompletionSource<object?> resultTcs) {
            UseHeadResponse = useHeadResponse;
            OutputFile = outputFile;
            ResultTcs = resultTcs;
        }

        public bool UseHeadResponse { get; }
        public string OutputFile { get; }
        public TaskCompletionSource<object?> ResultTcs { get; }
    }
}