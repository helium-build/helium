namespace Helium.DockerfileHandler.Commands
{
    public class ArgCommand
    {
        public ArgCommand(string name, string? defaultValue) {
            Name = name;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public string? DefaultValue { get; }
    }
}