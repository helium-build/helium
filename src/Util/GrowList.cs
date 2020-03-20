using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Helium.Util
{
    public struct GrowList<T>
        where T : class
    {
        private const int initialCapacity = 8;
        
        private GrowList(int count, T[] values) {
            this.count = count;
            this.values = values;
        }

        public GrowList(IEnumerable<T> lines) {
            values = lines.ToArray();
            count = values.Length;
        }
        
        
        private volatile int count;
        private volatile T[]? values;

        public void Add(T value) {
            if(values == null) {
                values = new T[initialCapacity];
            }
            else if(count >= values.Length) {
                var newValues = new T[values.Length * 2];
                Array.Copy(values, newValues, values.Length);
                values = newValues;
            }
            
            Volatile.Write(ref values[count], value);
            Interlocked.Increment(ref count);
        }

        public int Count => count;

        public T this[int index] {
            get {
                if(index < 0 || index >= count) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                
                return values![index];
            }
        }
        
        public Enumerator GetEnumerator() => new Enumerator(this);

        public static GrowList<string> Empty() =>
            new GrowList<string>(0, new string[initialCapacity]);
        
        
        public struct Enumerator
        {
            public Enumerator(GrowList<T> list) {
                this.list = list;
                i = -1;
            }
            
            private GrowList<T> list;
            private int i;

            public bool MoveNext() {
                ++i;
                return i < list.Count;
            }

            public T Current => list[i];

        }
    }
}