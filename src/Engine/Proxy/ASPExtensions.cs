using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Helium.Engine.Proxy
{
    public static class ASPExtensions
    {

        private static readonly string[] getHeadMethods = new[] {
            "GET",
            "HEAD",
        };
        
        public static void MapGetHead(this IEndpointRouteBuilder endpoint, string pattern, Func<HttpContext, bool, Task> request) {
            endpoint.MapMethods(pattern, getHeadMethods, async context => {
                var isGet = context.Request.Method == "GET";

                try {
                    await request(context, isGet);
                }
                catch(HttpErrorCodeException ex) {
                    context.Response.StatusCode = (int)ex.ErrorCode;
                }
            });
        }
        
        public static void PrepareJson(this HttpResponse response) {
            response.ContentType = "application/json";
        }

        
        public static async Task SendJson<T>(this HttpResponse response, T obj) {
            response.PrepareJson();
            var jsonStr = JsonConvert.SerializeObject(obj, typeof(T), new JsonSerializerSettings());
            await response.WriteAsync(jsonStr, Encoding.UTF8);
        }
        
    }
}