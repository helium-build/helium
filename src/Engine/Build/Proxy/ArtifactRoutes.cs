using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DotLiquid.Util;
using Helium.Engine.Conf;
using Helium.Engine.Build.Record;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.Engine.Build.Proxy
{
    internal class ArtifactRoutes
    {
        public ArtifactRoutes(IArtifactSaver artifact) {
            this.artifact = artifact;
        }

        private readonly IArtifactSaver artifact;

        public void Register(IEndpointRouteBuilder endpoint) {
            endpoint.MapPut("artifact/{fileName}", async context => {
                var fileName = (string)context.GetRouteValue("fileName");

                if(fileName.StartsWith(".") || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar)) {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                
                try {
                    var uploadFile = context.Request.Form.Files.First();
                    await using var uploadStream = uploadFile.OpenReadStream();
                    
                    await artifact.SaveArtifact(fileName, uploadStream);
                }
                catch(HttpErrorCodeException ex) {
                    context.Response.StatusCode = (int)ex.ErrorCode;
                }
            });
        }
        
    }
}