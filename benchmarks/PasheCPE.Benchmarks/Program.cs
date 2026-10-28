using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// En el mismo proceso: no genera ni compila proyectos auxiliares (más rápido y sin ejecutables nuevos para el antivirus).
var configuracion = DefaultConfig.Instance
    .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
    .AddDiagnoser(MemoryDiagnoser.Default);

BenchmarkSwitcher.FromAssembly(typeof(PasheCPE.Benchmarks.ComprobanteBenchmarks).Assembly).Run(args, configuracion);
