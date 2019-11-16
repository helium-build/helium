using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.Util
{
    public static class AsyncEnumerableExtensions
    {

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> elems) {
            var result = new List<T>();
            await foreach(var elem in elems) {
                result.Add(elem);
            }

            return result;
        }
        
    }
}