using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(SqlBound.Benchmarks.QueryBenchmarks).Assembly).Run(args);
