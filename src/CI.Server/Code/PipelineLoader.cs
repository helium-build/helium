using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using Helium.Pipeline;
using Helium.Sdks;
using Jint;
using Jint.Runtime.Interop;
using Microsoft.AspNetCore.Http;

namespace Helium.CI.Server
{
    public class PipelineLoader
    {
        private PipelineLoader(PipelineBuilderState builderState) {
            this.builderState = builderState;
        }
        
        private readonly PipelineBuilderState builderState;

        public IReadOnlyList<BuildArgInfo> Arguments => builderState.BuildArgs.ToList();
        
        public PipelineInfo BuildPipeline(IReadOnlyDictionary<string, string> arguments) =>
            builderState.PipelineBuilder?.Invoke(arguments) ?? throw new Exception("Pipeline was not set.");

        private void Load(string pipelineScript) {

            var engine = new Engine(options => options
                .Culture(CultureInfo.InvariantCulture)
                .TimeoutInterval(TimeSpan.FromSeconds(2))
            );
            
            

            dynamic console = new ExpandoObject();
            console.log = new Action<object>(ConsoleLog);

            engine.SetValue("console", console);

            engine.SetValue(nameof(SdkOperatingSystem), TypeReference.CreateTypeReference(engine, typeof(SdkOperatingSystem)));
            engine.SetValue(nameof(SdkArch), TypeReference.CreateTypeReference(engine, typeof(SdkArch)));
            engine.SetValue(nameof(PlatformInfo), TypeReference.CreateTypeReference(engine, typeof(PlatformInfo)));
            engine.SetValue(nameof(SdkHash), TypeReference.CreateTypeReference(engine, typeof(SdkHash)));

            
            engine.SetValue(nameof(BuildJob), TypeReference.CreateTypeReference(engine, typeof(BuildJob)));
            engine.SetValue(nameof(BuildTask), TypeReference.CreateTypeReference(engine, typeof(BuildTask)));
            engine.SetValue(nameof(GitBuildInput), TypeReference.CreateTypeReference(engine, typeof(GitBuildInput)));
            engine.SetValue(nameof(HttpRequestBuildInput), TypeReference.CreateTypeReference(engine, typeof(HttpRequestBuildInput)));
            engine.SetValue(nameof(ArtifactBuildInput), TypeReference.CreateTypeReference(engine, typeof(ArtifactBuildInput)));
            engine.SetValue(nameof(PipelineInfo), TypeReference.CreateTypeReference(engine, typeof(PipelineInfo)));
            
            engine.SetValue("helium", builderState);


            engine.Execute(pipelineScript);
        }

        private static void ConsoleLog(object obj) {
            foreach(var line in (obj?.ToString() ?? "").Split("\n")) {
                Console.WriteLine("Pipeline: {0}", line);
            }
        }

        public static PipelineLoader Create(string pipelineScript) {
            var builderState = new PipelineBuilderState();
            var loader = new PipelineLoader(builderState);
            loader.Load(pipelineScript);
            return loader;
        }
        
        
    }
}