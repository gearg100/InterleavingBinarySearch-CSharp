# Interleaving Binary Search in C# (.Net Core 3.0)
Two implementations of interleaved execution for binary search in CSharp using:
- async/await, and
- yield.

## Build
Assuming .Net Core 3.0 installed:
``` ini
> dotnet build --configuration Release
```

## Benchmark results
N: array size, V: number of values to look for, G: group size

``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7500U CPU 2.70GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.0.100-preview5-011568
  [Host] : .NET Core 3.0.0-preview5-27626-15 (CoreCLR 4.6.27622.75, CoreFX 4.700.19.22408), 64bit RyuJIT
  Core   : .NET Core 3.0.0-preview5-27626-15 (CoreCLR 4.6.27622.75, CoreFX 4.700.19.22408), 64bit RyuJIT

Job=Core  Runtime=Core  

```
|                Method |          N |     V |  G |      Mean |     Error |    StdDev |    Median | Rank |
|---------------------- |----------- |------ |--- |----------:|----------:|----------:|----------:|-----:|
|            Sequential |    1048576 | 10000 | 10 |  2.520 ms | 0.0500 ms | 0.1096 ms |  2.453 ms |    **1** |
|       InterleavedTask |    1048576 | 10000 | 10 | 21.465 ms | 0.3984 ms | 0.4092 ms | 21.404 ms |    3 |
| InterleavedEnumerable |    1048576 | 10000 | 10 |  2.720 ms | 0.0276 ms | 0.0231 ms |  2.720 ms |    2 |
|                       |            |       |    |           |           |           |           |      |
|            Sequential | 1073741824 | 10000 | 10 | 17.501 ms | 0.3824 ms | 1.1217 ms | 17.141 ms |    2 |
|       InterleavedTask | 1073741824 | 10000 | 10 | 34.454 ms | 0.6842 ms | 0.8653 ms | 34.425 ms |    3 |
| InterleavedEnumerable | 1073741824 | 10000 | 10 |  6.558 ms | 0.0359 ms | 0.0300 ms |  6.550 ms |    **1** |
