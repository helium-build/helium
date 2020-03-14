namespace Helium.CI.Agent
{
    public enum AgentState
    {
        Initial,
        UploadingWorkspace,
        RunningBuild,
        PostBuild,
    }
}