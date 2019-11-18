using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Helium.Util
{
    public static class HttpUtil
    {
        public static async Task<string> FetchString(string url) {
            var request = WebRequest.CreateHttp(url);

            using var response = (HttpWebResponse)await request.GetResponseAsync();
            await using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public static async Task<T> FetchJson<T>(string url) =>
            JsonConvert.DeserializeObject<T>(await FetchString(url));

        public static async Task FetchWriteStream(string url, Stream stream) {
            var request = WebRequest.CreateHttp(url);

            using var response = (HttpWebResponse)await request.GetResponseAsync();
            await using var inStream = response.GetResponseStream();
            await inStream.CopyToAsync(stream);
        }
        
        public static async Task FetchFile(string url, string fileName) {
            await using var stream = File.Create(fileName);
            await FetchWriteStream(url, stream);
        }
    }
}