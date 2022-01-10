using DotNext;
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static DotNext.Threading.AtomicInt64;

namespace RaftNode;

internal sealed class SimplePersistentState : MemoryBasedStateMachine, ISupplier<BigStruct>
{
    internal const string LogLocation = "logLocation";

    private sealed class SimpleSnapshotBuilder : IncrementalSnapshotBuilder
    {
        private BigStruct value;

        public SimpleSnapshotBuilder(in SnapshotBuilderContext context)
            : base(context)
        {
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
            => value = await entry.ToTypeAsync<BigStruct, LogEntry>().ConfigureAwait(false);

        public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(value, token);
    }

    private readonly object contentLock = new ();
    private BigStruct content;
    private readonly Stopwatch timeTo1kValues = new();

    public SimplePersistentState(string path, AppEventSource source)
        : base(path, 1000, CreateOptions(source))
    {
    }

    public SimplePersistentState(IConfiguration configuration, AppEventSource source)
        : this(configuration[LogLocation], source)
    {
    }

    private static Options CreateOptions(AppEventSource source)
    {
        var result = new Options
        {   
            BufferSize = 4096 * 1000,
            InitialPartitionSize = 50 * 8 * 1000,
            CompactionMode = CompactionMode.Sequential,//sequential is the default
            WriteCounter = new("WAL.Writes", source),
            ReadCounter = new("WAL.Reads", source),
            CommitCounter = new("WAL.Commits", source),
            CompactionCounter = new("WAL.Compaction", source),
            LockContentionCounter = new("WAL.LockContention", source),
            LockDurationCounter = new("WAL.LockDuration", source),
        };

        result.WriteCounter.DisplayUnits =
            result.ReadCounter.DisplayUnits =
            result.CommitCounter.DisplayUnits =
            result.CompactionCounter.DisplayUnits = "entries";

        result.LockDurationCounter.DisplayUnits = "milliseconds";
        result.LockDurationCounter.DisplayName = "WAL Lock Duration";

        result.LockContentionCounter.DisplayName = "Lock Contention";

        result.WriteCounter.DisplayName = "Number of written entries";
        result.ReadCounter.DisplayName = "Number of retrieved entries";
        result.CommitCounter.DisplayName = "Number of committed entries";
        result.CompactionCounter.DisplayName = "Number of squashed entries";

        return result;
    }

    BigStruct ISupplier<BigStruct>.Invoke() 
    { 
        lock (contentLock) return content; 
    }

    private async ValueTask UpdateValue(LogEntry entry)
    {
        var value = await entry.ToTypeAsync<BigStruct, LogEntry>().ConfigureAwait(false);
        lock (contentLock)
            content = value;
        if (value.Field1 % 1000 == 0)
        {
            Console.WriteLine($"Accepting value {value.Field1} - time since last 1k value: {timeTo1kValues.Elapsed}");
            timeTo1kValues.Restart();
        }
    }

    protected override ValueTask ApplyAsync(LogEntry entry)
        => entry.Length == 0L ? new ValueTask() : UpdateValue(entry);

    protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
    {
        Console.WriteLine("Building snapshot");
        return new SimpleSnapshotBuilder(context);
    }
}