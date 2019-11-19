using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Helium.Engine.Conf;
using Helium.Engine.Record;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Helium.Engine.Proxy
{
    internal class MavenRoutes
    {
        private MavenRoutes(Dictionary<string, MavenProxy> proxies) {
            this.proxies = proxies;
        }

        private const string pathPartFormat = @"[a-z-A-Z0-9_\-][a-z-A-Z0-9_\-\.]*";
        private static readonly Regex pathRegex = new Regex("^" + pathPartFormat + "(/" + pathPartFormat + ")*$");
        private static readonly string[] GetHeadMethods = new[] {
            "GET",
            "HEAD",
        };
        
        private readonly Dictionary<string, MavenProxy> proxies;

        public void Register(IEndpointRouteBuilder endpoint) {
            endpoint.MapMethods("maven/{proxyName}/{**path}", GetHeadMethods, async context => {
                var proxyName = (string)context.GetRouteValue("proxyName");
                var path = (string)context.GetRouteValue("path");
                if(!proxies.TryGetValue(proxyName, out var proxy)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                if(!pathRegex.IsMatch(path)) {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var file = await proxy.GetArtifact(path);
                if(context.Request.Method == "GET") {
                    await context.Response.SendFileAsync(file);
                }
            });
            
            
        }

        public static MavenRoutes Build(IRecorder recorder, Config config) {
            var proxies = new Dictionary<string, MavenProxy>();
            foreach(var repo in config.repos.maven) {
                var name = repo.name ?? throw new Exception("Maven repo name is missing.");
                var url = repo.url ?? throw new Exception("Maven repo url is missing.");
                
                proxies.Add(name, new MavenProxy(recorder, name, url));
            }
            
            return new MavenRoutes(proxies);
        }
    }
}