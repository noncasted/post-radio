using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Tests.Execution;

public class TaskQueueTests
{
    private readonly ITaskQueue _queue;

    public TaskQueueTests()
    {
        var logger = Substitute.For<ILogger<TaskQueue>>();
        _queue = new TaskQueue(logger);
    }

    [Fact]
    public void Collect_EmptyQueue_ReturnsEmpty()
    {
        var result = _queue.Collect();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Enqueue_NoDelay_AvailableImmediately()
    {
        var task = new FakeTask("t1", delay: TimeSpan.Zero);

        _queue.Enqueue(task);
        var result = _queue.Collect();

        result.Should()
              .ContainSingle()
              .Which.Id.Should()
              .Be("t1");
    }

    [Fact]
    public void Enqueue_WithDelay_NotAvailableBeforeExpiry()
    {
        var task = new FakeTask("t1", delay: TimeSpan.FromMinutes(10));

        _queue.Enqueue(task);
        var result = _queue.Collect();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Collect_RemovesReadyTasksFromQueue()
    {
        var task = new FakeTask("t1", delay: TimeSpan.Zero);

        _queue.Enqueue(task);
        _queue.Collect();
        var secondCollect = _queue.Collect();

        secondCollect.Should().BeEmpty();
    }

    [Fact]
    public void Collect_LeavesDelayedTasksInQueue()
    {
        var task = new FakeTask("delayed", delay: TimeSpan.FromMinutes(10));

        _queue.Enqueue(task);

        // First collect: not ready
        _queue.Collect().Should().BeEmpty();

        // Task remains — if we enqueue a ready one, only it returns
        var ready = new FakeTask("ready", delay: TimeSpan.Zero);
        _queue.Enqueue(ready);

        var result = _queue.Collect();

        result.Should()
              .ContainSingle()
              .Which.Id.Should()
              .Be("ready");
    }

    [Fact]
    public void Enqueue_SameId_DoesNotOverwrite()
    {
        // TryAdd is used internally — same ID is silently ignored
        var first = new FakeTask("t1", delay: TimeSpan.Zero, priority: TaskPriority.Low);
        var second = new FakeTask("t1", delay: TimeSpan.FromMinutes(10), priority: TaskPriority.Critical);

        _queue.Enqueue(first);
        _queue.Enqueue(second);

        // First enqueue wins (TryAdd), so the task is immediately available
        var result = _queue.Collect();

        result.Should()
              .ContainSingle()
              .Which.Priority.Should()
              .Be(TaskPriority.Low);
    }

    [Fact]
    public void Collect_MultipleTasks_ReturnsOnlyReady()
    {
        var ready1 = new FakeTask("r1", delay: TimeSpan.Zero);
        var ready2 = new FakeTask("r2", delay: TimeSpan.Zero);
        var delayed = new FakeTask("d1", delay: TimeSpan.FromMinutes(10));

        _queue.Enqueue(ready1);
        _queue.Enqueue(ready2);
        _queue.Enqueue(delayed);

        var result = _queue.Collect();

        result.Should().HaveCount(2);
        result.Select(t => t.Id).Should().BeEquivalentTo("r1", "r2");
    }

    [Fact]
    public void ConcurrentEnqueue_AllTasksCollected()
    {
        const int taskCount = 100;

        var tasks = Enumerable.Range(0, taskCount)
                              .Select(i => new FakeTask($"t{i}", delay: TimeSpan.Zero))
                              .ToList();

        Parallel.ForEach(tasks, task => _queue.Enqueue(task));

        var result = _queue.Collect();
        result.Should().HaveCount(taskCount);
    }

    [Fact]
    public void ConcurrentEnqueueAndCollect_AllTasksEventuallyCollected()
    {
        const int iterations = 50;
        var exceptions = new List<Exception>();
        var collected = new List<IReadOnlyList<IPriorityTask>>();

        var enqueueTask = Task.Run(() => {
            for (var i = 0; i < iterations; i++)
            {
                try
                {
                    _queue.Enqueue(new FakeTask($"t{i}", delay: TimeSpan.Zero));
                }
                catch (Exception e)
                {
                    lock (exceptions)
                        exceptions.Add(e);
                }
            }
        });

        var collectTask = Task.Run(() => {
            for (var i = 0; i < iterations; i++)
            {
                try
                {
                    var batch = _queue.Collect();

                    if (batch.Count > 0)
                    {
                        lock (collected)
                            collected.Add(batch);
                    }
                }
                catch (Exception e)
                {
                    lock (exceptions)
                        exceptions.Add(e);
                }
            }
        });

        Task.WaitAll(enqueueTask, collectTask);
        exceptions.Should().BeEmpty();

        // Drain any remaining tasks after concurrent phase
        var remaining = _queue.Collect();

        if (remaining.Count > 0)
            collected.Add(remaining);

        var totalCollected = collected.SelectMany(b => b).Select(t => t.Id).ToList();
        totalCollected.Should().OnlyHaveUniqueItems("each task should be collected exactly once");

        totalCollected.Should()
                      .HaveCount(iterations,
                          "all enqueued tasks should eventually be collected");
    }

    [Fact]
    public void Enqueue_AfterCollect_SameIdCanBeReAdded()
    {
        var task = new FakeTask("t1", delay: TimeSpan.Zero);

        _queue.Enqueue(task);
        _queue.Collect();

        // After collection removes the task, same ID can be added again
        _queue.Enqueue(task);
        var result = _queue.Collect();

        result.Should()
              .ContainSingle()
              .Which.Id.Should()
              .Be("t1");
    }
}

public class FakeTask : IPriorityTask
{
    public FakeTask(string id, TimeSpan delay, TaskPriority priority = TaskPriority.Medium)
    {
        Id = id;
        Delay = delay;
        Priority = priority;
    }

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay { get; }

    private int _executeCount;
    public int ExecuteCount => _executeCount;

    private bool _shouldFail;

    public FakeTask SetShouldFail(bool shouldFail)
    {
        _shouldFail = shouldFail;
        return this;
    }

    public Task Execute()
    {
        Interlocked.Increment(ref _executeCount);

        if (_shouldFail)
            throw new InvalidOperationException("Task failed");

        return Task.CompletedTask;
    }
}