using System;
using System.Buffers.Text;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Helium.Sdks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

namespace SdkServer
{
    public class Endpoints
    {
        public Endpoints(ISdkResolver resolver) {
            this.resolver = resolver;
        }
        
        private readonly ISdkResolver resolver;

        public void Register(IEndpointRouteBuilder endpoints) {
            endpoints.MapGet("/sdk/{sdk}/versions", ListSdkVersions);
            endpoints.MapGet("/sdk/{sdk}/v/{version}/platforms", ListSdkPlatforms);
            endpoints.MapGet("/sdk/{sdk}/v/{version}/platform/{platform}/sdk.json", GetSdkJson);
            endpoints.MapGet("/sdk/{sdk}/v/{version}/platform/{platform}/file/{fileNum:int}", GetSdkFile);
        }

        
        private Task ListSdkVersions(HttpContext context) =>
            WriteJson(context,
                resolver.GetSdk((string)context.GetRouteValue("sdk"))?.Versions.ToList()
            );

        private Task ListSdkPlatforms(HttpContext context) =>
            WriteJson(context,
                resolver
                    .GetSdk((string)context.GetRouteValue("sdk"))
                    ?.GetVersion((string)context.GetRouteValue("version"))
                    ?.Platforms.ToList()
            );

        private async Task GetSdkJson(HttpContext context) {
            var platform = GetPlatform(context);

            if(platform == null) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var urlBuilder = new UriBuilder(context.Request.Scheme, context.Request.Host.Host);

            if(context.Request.Host.Port is int port) {
                urlBuilder.Port = port;
            }

            urlBuilder.Path = context.Request.Path;
            
            await WriteJson(context,
                resolver
                    .GetSdk((string)context.GetRouteValue("sdk"))
                    ?.GetVersion((string)context.GetRouteValue("version"))
                    ?.GetPlatform(platform)
                    ?.SdkForBaseUrl(urlBuilder.Uri)
            );
        }

        private async Task GetSdkFile(HttpContext context) {
            var platform = GetPlatform(context);

            if(platform == null) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var index = (int)context.GetRouteValue("fileNum");

            if(index < 0) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var url = resolver
                .GetSdk((string)context.GetRouteValue("sdk"))
                ?.GetVersion((string)context.GetRouteValue("version"))
                ?.GetPlatform(platform)
                ?.UrlForFile(index);

            if(url == null) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            
            context.Response.Redirect(url);
        }


        private PlatformInfo? GetPlatform(HttpContext context) {
            try {
                var platformEnc = (string)context.GetRouteValue("platform");
                var platformBytes = Base64UrlTextEncoder.Decode(platformEnc);
                var platformStr = Encoding.UTF8.GetString(platformBytes);
                return JsonConvert.DeserializeObject<PlatformInfo>(platformStr);
            }
            catch {
                return null;
            }
        }
        
        private const string JsonContentType = "application/json";

        private async Task WriteJson<T>(HttpContext context, T? value) where T : class {
            if(value == null) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            
            var json = JsonConvert.SerializeObject(value, typeof(T), new JsonSerializerSettings());
            context.Response.ContentType = JsonContentType;
            await context.Response.WriteAsync(json);
        }
        
    }
}