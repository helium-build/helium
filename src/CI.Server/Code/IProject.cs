using System.Threading.Tasks;

namespace Helium.CI.Server
{
    public interface IProject
    {
        ProjectConfig Config { get; }
        Task UpdateConfig(ProjectConfig config);
    }
}