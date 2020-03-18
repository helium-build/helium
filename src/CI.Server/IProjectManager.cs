using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IProjectManager
    {
        IProject? GetProject(Guid id);
        
        IReadOnlyCollection<IProject> Projects { get; }

        Task<IProject> AddProject(ProjectConfig config);
        Task RemoveProject(IProject project);
    }
}