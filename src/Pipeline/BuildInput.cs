using System;
using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.Pipeline
{
    public abstract class BuildInput
    {
        internal BuildInput() {}
        
        public abstract string Path { get; }
    }

    public sealed class GitBuildInput : BuildInput
    {
        public GitBuildInput(string url, string path, string? @ref) {
            Url = url;
            Path = path;
            Ref = @ref ?? throw new ArgumentNullException(nameof(@ref));
        }
        
        public GitBuildInput(IDictionary<string, object> obj)
            : this(
                url: (string)obj["url"],
                path: (string)obj["path"],
                @ref: (string)obj["ref"]
            ) {}
        
        public string Url { get; }
        public string? Ref { get; }
        
        public override string Path { get; }
    }

    public sealed class HttpRequestBuildInput : BuildInput
    {
        public HttpRequestBuildInput(string url, SdkHash hash, string path) {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
        
        public HttpRequestBuildInput(IDictionary<string, object> obj)
            : this(
                url: (string)obj["url"],
                hash: (SdkHash)obj["hash"],
                path: (string)obj["path"]
            ) {}
        
        public string Url { get; }
        public SdkHash Hash { get; }
        
        public override string Path { get; }
    }

    public sealed class ArtifactBuildInput : BuildInput
    {
        public ArtifactBuildInput(BuildJob job, string artifactPath, string path) {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            ArtifactPath = artifactPath ?? throw new ArgumentNullException(nameof(job));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public ArtifactBuildInput(IDictionary<string, object> obj)
            : this(
                job: (BuildJob)obj["job"],
                artifactPath: (string)obj["artifactPath"],
                path: (string)obj["path"]
            ) {}

        public BuildJob Job { get; }
        public string ArtifactPath { get; }

        public override string Path { get; }
    }

}