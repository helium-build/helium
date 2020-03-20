using Helium.Util;

namespace Helium.CI.Server
{
    public class OutputLinesChangedEventArgs
    {
        public OutputLinesChangedEventArgs(GrowList<string> lines) {
            Lines = lines;
        }

        public GrowList<string> Lines { get; }
    }
}