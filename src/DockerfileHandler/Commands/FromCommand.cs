namespace Helium.DockerfileHandler.Commands
{
    public class FromCommand
    {
        public FromCommand(string image, string? asName) {
            Image = image;
            AsName = asName;
        }

        public string Image { get; }
        public string? AsName { get; }
    }
}