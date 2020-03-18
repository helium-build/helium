namespace Helium.CI.Server
{
    public class ProjectConfig
    {
        public ProjectConfig(string name, string url, string branch, string path) {
            Name = name;
            Url = url;
            Branch = branch;
            Path = path;
        }

        public string Name { get; }
        public string Url { get; }
        public string Branch { get; }
        public string Path { get; }
    }
}