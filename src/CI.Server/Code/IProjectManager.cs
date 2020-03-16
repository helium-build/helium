using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IProjectManager
    {
        Task<IProject?> GetProject(string id);
        IAsyncEnumerable<IProject> AllProjects();

        Task<IProject> AddProject(string id, ProjectConfig config);
        Task RemoveProject(IProject project);
    }
}