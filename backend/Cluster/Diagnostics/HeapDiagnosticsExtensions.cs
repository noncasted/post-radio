using Cluster.Diagnostics;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Cluster;

public static class HeapDiagnosticsExtensions
{
    public static IHostApplicationBuilder AddHeapDiagnostics(this IHostApplicationBuilder builder)
    {
        builder.Add<HeapSnapshotCollector>()
               .As<IHeapSnapshotCollector>();

        builder.Add<HeapSnapshotEndpoint>()
               .As<ILocalSetupCompleted>();

        return builder;
    }
}
