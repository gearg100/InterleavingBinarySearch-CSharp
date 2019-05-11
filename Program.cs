using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Runtime.CompilerServices;

namespace BinarySearch
{
    static class Impl
    {
        public static int[] array;
        public static int[] values;

        public static void Setup(int N, int V)
        {
            array = new int[N];
            for (int i = 0; i < N; i++)
            {
                array[i] = i;
            }
            values = new int[V];
            var rng = new Random();
            for (int i = 0; i < V; i++)
            {
                values[i] = rng.Next(N);
            }
        }

        public static void Sequential(int V, int _, int[] array, int[] values)
        {
            List<int> res = new List<int> { };
            res.Capacity = V;
            foreach (var val in values)
            {
                int low = 0, size = array.Length;

                while (size > 1)
                {
                    int probe = low + size / 2;
                    int v = array[probe];
                    if (v < val) low = probe;
                    size -= size / 2;
                }

                if (size == 1 && array[low] < val) low++;
                res.Add(low);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void Prefetch(int[] arr, int idx)
        {
            fixed (int* ptr = arr)
            {
                Sse.Prefetch0(ptr + idx);
            }
        }

        public static void InterleavedTask(int V, int G, int[] array, int[] values)
        {
            List<int> res = new List<int> { };
            res.Capacity = V;
            var tasks = Enumerable.Range(0, G).Select(async (i) =>
            {
                while (i < V)
                {
                    var idx = i; i += G;
                    var val = values[idx];
                    int low = 0, size = array.Length;

                    while (size > 1)
                    {
                        int probe = low + size / 2;
                        Prefetch(array, probe);
                        await Task.Yield();
                        int v = array[probe];
                        if (v < val) low = probe;
                        size -= size / 2;
                    }

                    if (size == 1 && array[low] < val) low++;
                    res.Add(low);
                }
            });
            Task.WaitAll(tasks.ToArray());
        } 

        class Res<T>
        {
            public T Value;
        }

        static IEnumerable<ValueTuple> ManySearches(int G, int[] array, int[] values, List<int> res, int i)
        {
            while (i < values.Length)
            {
                var idx = i; i += G;
                var val = values[idx];
                int low = 0, size = array.Length;

                while (size > 1)
                {
                    int probe = low + size / 2;
                    Prefetch(array, probe);
                    yield return new ValueTuple();
                    int v = array[probe];
                    if (v < val) low = probe;
                    size -= size / 2;
                }

                if (size == 1 && array[low] < val) low++;
                res.Add(low);
            }
        }


        public static void InterleavedEnumerable(int V, int G, int[] array, int[] values)
        {
            List<int> res = new List<int> { };
            res.Capacity = V;
            var tasks = Enumerable.Range(0, G).Select((i) => ManySearches(G, array, values, res, i).GetEnumerator()).ToArray();
            int remaining = G;
            while (G > 0)
            {
                for(int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i] == null) continue;
                    if (!tasks[i].MoveNext())
                    {
                        tasks[i] = null;
                        G--;
                    }
                }
            }
        }
    }


    [CoreJob]
    [RPlotExporter, RankColumn]
    public class Bench
    {
        [Params(1 * 1024 * 1024, 1 * 1024 * 1024 * 1024)]
        public int N;

        [Params(10000)]
        public int V;

        [Params(10)]
        public int G;

        [GlobalSetup]
        public void Setup() => Impl.Setup(N, V);

        [Benchmark]
        public void Sequential() => Impl.Sequential(V, G, Impl.array, Impl.values);

        [Benchmark]
        public void InterleavedTask() => Impl.InterleavedTask(V, G, Impl.array, Impl.values);

        [Benchmark]
        public void InterleavedEnumerable() => Impl.InterleavedEnumerable(V, G, Impl.array, Impl.values);

    }
    class Program
    {        
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Bench>();
        }
    }
}
