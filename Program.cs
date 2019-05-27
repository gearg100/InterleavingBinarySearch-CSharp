using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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
        public static List<int> resBaseline;
        public static List<int> resTask;
        public static List<int> resEnumerable;
        public static List<int> resSeqEnumerable;
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

        public static void Sequential(int V, int[] array, int[] values)
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
                    if (v == val)
                    {
                        res.Add(probe); goto lbl;
                    }
                    if (v < val) low = probe;
                    size -= size / 2;
                }

                if (size == 1 && array[low] < val) low++;
                res.Add(low);
            lbl:
                ;
            }

            resBaseline = res;
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
            lbl:
                while (i < V)
                {
                    var idx = i; i += G;
                    var val = values[idx];
                    int low = 0, size = array.Length;

                    while (size > 1)
                    {
                        int probe = low + size / 2;
                        if (G > 1)
                        {
                            Prefetch(array, probe);
                            await Task.Yield();
                        }
                        int v = array[probe];
                        if (v == val)
                        {
                            res.Add(probe); goto lbl;
                        }
                        if (v < val) low = probe;
                        size -= size / 2;
                    }

                    if (size == 1 && array[low] < val) low++;
                    res.Add(low);
                }
            });
            Task.WaitAll(tasks.ToArray());

            resTask = res;
        }

        static IEnumerable<ValueTuple> ManySearches(int V, int G, int[] array, int[] values, List<int> res, int i)
        {
        lbl:
            while (i < V)
            {
                var idx = i; i += G;
                var val = values[idx];
                int low = 0, size = array.Length;

                while (size > 1)
                {
                    int probe = low + size / 2;
                    if (G > 1)
                    {
                        Prefetch(array, probe);
                        yield return new ValueTuple();
                    }
                    int v = array[probe];
                    if (v == val)
                    {
                        res.Add(probe); goto lbl;
                    }
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
            var tasks = Enumerable.Range(0, G).Select((i) => ManySearches(V, G, array, values, res, i).GetEnumerator()).ToArray();
            int remaining = G;
            while (remaining > 0)
            {
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i] == null) continue;
                    if (!tasks[i].MoveNext())
                    {
                        tasks[i] = null;
                        remaining--;
                    }
                }
            }

            if (G > 1)
            {
                resEnumerable = res;
            }
            else
            {
                resSeqEnumerable = res;
            }
        }
    }


    [CoreJob]
    [RPlotExporter, RankColumn]
    public class Bench
    {
        [Params(1 * 1024, 1 * 1024 * 1024, 1 * 1024 * 1024 * 1024)]
        public int N;

        [Params(10000)]
        public int V;

        [GlobalSetup]
        public void Setup() => Impl.Setup(N, V);

        [Benchmark(Baseline = true)]
        [Arguments(1)]
        public void Sequential(int G) => Impl.Sequential(V, Impl.array, Impl.values);

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        public void InterleavedEnumerable(int G) => Impl.InterleavedEnumerable(V, G, Impl.array, Impl.values);

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        public void InterleavedTask(int G) => Impl.InterleavedTask(V, G, Impl.array, Impl.values);

    }
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] != "test")
            {
                var summary = BenchmarkRunner.Run<Bench>();
                return;
            }
            int N = 10;
            int V = 10;
            int G = 5;

            Impl.Setup(N, V);

            foreach (var r in Impl.array)
            {
                Console.Write(r + " ");
            }
            Console.WriteLine();
            foreach (var r in Impl.values)
            {
                Console.Write(r + " ");
            }
            Console.WriteLine(); Console.WriteLine();

            Impl.Sequential(V, Impl.array, Impl.values);
            Impl.InterleavedTask(V, G, Impl.array, Impl.values);
            Impl.InterleavedEnumerable(V, G, Impl.array, Impl.values);

            Impl.resBaseline.Sort(); Impl.resTask.Sort();
            Impl.resEnumerable.Sort(); Impl.resSeqEnumerable.Sort();
            foreach (var r in Impl.resBaseline)
            {
                Console.Write(r + " ");
            }
            Console.WriteLine();
            foreach (var r in Impl.resTask)
            {
                Console.Write(r + " ");
            }
            Console.WriteLine();
            foreach (var r in Impl.resEnumerable)
            {
                Console.Write(r + " ");
            }
            Console.WriteLine();
        }
    }
}

