using System.Collections;
using System.Collections.Generic;

namespace Helium.Util
{
    public class ReadOnlyCollectionNoList<T> : IReadOnlyCollection<T>
    {
        public ReadOnlyCollectionNoList(ICollection<T> values) {
            this.values = values;
        }
        
        private readonly ICollection<T> values;

        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();

        public int Count => values.Count;
    }
}