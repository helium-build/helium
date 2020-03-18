using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IProjectManager
    {
        Task<IProject?> GetProject(string id);
        
        IReadOnlyCollection<IProject> Projects { get; }

        Task<IProject> AddProject(string id, ProjectConfig config);
        Task RemoveProject(IProject project);
    }
}