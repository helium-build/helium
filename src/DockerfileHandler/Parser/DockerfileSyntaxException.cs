using System;

namespace Helium.DockerfileHandler.Parser
{
    public class DockerfileSyntaxException : Exception
    {
        public DockerfileSyntaxException(string message) : base(message) {
        }
    }
}