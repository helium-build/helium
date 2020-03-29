namespace Helium.DockerfileHandler.Commands
{
    public abstract class CommandBase
    {
        internal CommandBase() {}
        
        public abstract string CommandName { get; }
    }
}