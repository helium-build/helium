using System;
using System.IO;

namespace Helium.Env
{
    public class Directories
    {
        public static string AppDir { get; } =
            Environment.GetEnvironmentVariable("HELIUM_BASE_DIR") is {} appDir
                ? appDir
                : Environment.CurrentDirectory;

        public static string ConfDir { get; } =
            Path.Combine(AppDir, "conf");

        public static string CacheDir { get; } =
            Path.Combine(AppDir, "cache");

        public static string SdkDir { get; } =
            Path.Combine(AppDir, "sdks");

        public static string AgentWorkspacesDir { get; } =
            Environment.GetEnvironmentVariable("HELIUM_AGENT_WORKSPACES_DIR") ?? Path.Combine(AppDir, "workspaces");
        
        public static string EngineContentRoot { get; } =
            Environment.GetEnvironmentVariable("HELIUM_ENGINE_CONTENT_ROOT") ?? AppDir;
        
        public static string AgentContentRoot { get; } =
            Environment.GetEnvironmentVariable("HELIUM_AGENT_CONTENT_ROOT") ?? AppDir;
        
        
    }
}