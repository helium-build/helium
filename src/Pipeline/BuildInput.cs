using System;
using System.Collections.Generic;
using Helium.Sdks;

namespace Helium.Pipeline
{
    public sealed class BuildInput
    {
        internal BuildInput(BuildInputSource source, string path) {
            Source = source;
            Path = path;
        }

        public BuildInputSource Source { get; }
        public string Path { get; }
    }
    
    public abstract class BuildInputSource
    {
        internal BuildInputSource() {}
    }

    public sealed class GitBuildInput : BuildInputSource
    {
        public GitBuildInput(string url, string? @ref) {
            Url = url;
            Ref = @ref ?? throw new ArgumentNullException(nameof(@ref));
        }
        
        public GitBuildInput(IDictionary<string, object> obj)
            : this(
                url: (string)obj["url"],
                @ref: (string)obj["ref"]
            ) {}
        
        public string Url { get; }
        public string? Ref { get; }

        public override bool Equals(object? obj) =>
            obj is GitBuildInput other &&
                Url == other.Url &&
                Ref == other.Ref;


        public override int GetHashCode() =>
            HashCode.Combine(Url, Ref);
    }

    public sealed class HttpRequestBuildInput : BuildInputSource
    {
        public HttpRequestBuildInput(string url, SdkHash hash) {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
        
        public HttpRequestBuildInput(IDictionary<string, object> obj)
            : this(
                url: (string)obj["url"],
                hash: (SdkHash)obj["hash"]
            ) {}
        
        public string Url { get; }
        public SdkHash Hash { get; }

        public override bool Equals(object? obj) =>
            obj is HttpRequestBuildInput other &&
                Url == other.Url &&
                Hash.Equals(other.Hash);

        public override int GetHashCode() =>
            HashCode.Combine(Url, Hash);
    }

    public sealed class ArtifactBuildInput : BuildInputSource
    {
        public ArtifactBuildInput(BuildJob job, string artifactPath) {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            ArtifactPath = artifactPath ?? throw new ArgumentNullException(nameof(job));
        }

        public ArtifactBuildInput(IDictionary<string, object> obj)
            : this(
                job: (BuildJob)obj["job"],
                artifactPath: (string)obj["artifactPath"]
            ) {}

        public BuildJob Job { get; }
        public string ArtifactPath { get; }
    }

}