using Common.Reactive;
using FluentAssertions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Tests.Execution;

public class TaskBalancerTests
{
    private readonly TaskQueue _queue;
    private readonly TaskBalancer _balancer;

    public TaskBalancerTests()
    {
        var queueLogger = Substitute.For<ILogger<TaskQueue>>();
        var balancerLogger = Substitute.For<ILogger<TaskBalancer>>();
        _queue = new TaskQueue(queueLogger);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = 10
        });
        _balancer = new TaskBalancer(_queue, balancerLogger, config);
    }

    [Fact]
    public async Task CriticalPriority_ExecutesBeforeLow()
    {
        var executionOrder = new List<string>();

        var lowTask = new OrderTrackingTask("low", TaskPriority.Low, executionOrder);
        var criticalTask = new OrderTrackingTask("critical", TaskPriority.Critical, executionOrder);

        _queue.Enqueue(lowTask);
        _queue.Enqueue(criticalTask);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 1,
            ExceptionPenalty = 50,
            ConcurrentTasks = 1
        });
        var logger = Substitute.For<ILogger<TaskBalancer>>();
        var balancer = new TaskBalancer(_queue, logger, config);

        var lifetime = new Lifetime();
        balancer.Run(lifetime);

        await WaitUntil(() => executionOrder.Count >= 2, timeoutMs: 3000);

        lifetime.Terminate();

        executionOrder[0].Should().Be("critical");
        executionOrder[1].Should().Be("low");
    }

    [Fact]
    public async Task FailedTask_ReEnqueuedToQueue()
    {
        var task = new FakeTask("failing", delay: TimeSpan.Zero, priority: TaskPriority.Medium)
            .SetShouldFail(true);

        _queue.Enqueue(task);

        var lifetime = new Lifetime();
        _balancer.Run(lifetime);

        await WaitUntil(() => task.ExecuteCount >= 2, timeoutMs: 3000);

        lifetime.Terminate();

        task.ExecuteCount.Should()
            .BeGreaterThanOrEqualTo(2,
                "failed task should be re-enqueued and retried");
    }

    [Fact]
    public async Task AgingMechanism_OlderTasksGetHigherScore()
    {
        var executionOrder = new List<string>();
        var lowTask = new OrderTrackingTask("low-old", TaskPriority.Low, executionOrder);
        _queue.Enqueue(lowTask);

        var config = CreateConfig(new TaskBalancerOptions
        {
            EmptyDelayMs = 10,
            NextDelayMs = 10,
            IterationScore = 100,
            ExceptionPenalty = 50,
            ConcurrentTasks = 1
        });
        var logger = Substitute.For<ILogger<TaskBalancer>>();
        var balancer = new TaskBalancer(_queue, logger, config);

        var lifetime = new Lifetime();
        balancer.Run(lifetime);

        // Let the low task age through several collect cycles (gains IterationScore=100 each cycle)
        await Task.Delay(200);

        var highTask = new OrderTrackingTask("high-new", TaskPriority.High, executionOrder);

        _queue.Enqueue(highTask);

        await WaitUntil(() => executionOrder.Count >= 2, timeoutMs: 3000);

        lifetime.Terminate();

        // Aged low-priority task should execute before fresh high-priority task
        // because accumulated aging score (100 * N cycles) > High base score (30)
        executionOrder.Should().HaveCount(2);
        executionOrder[0].Should().Be("low-old", "aged low-priority task should beat fresh high-priority");
        executionOrder[1].Should().Be("high-new");
    }

    [Fact]
    public async Task MultipleTasks_AllExecutedExactlyOnce()
    {
        var tasks = Enumerable.Range(0, 5)
                              .Select(i => new FakeTask($"t{i}", delay: TimeSpan.Zero, priority: TaskPriority.Medium))
                              .ToList();

        foreach (var task in tasks)
            _queue.Enqueue(task);

        var lifetime = new Lifetime();
        _balancer.Run(lifetime);

        await WaitUntil(() => tasks.All(t => t.ExecuteCount >= 1), timeoutMs: 3000);

        lifetime.Terminate();

        // Each task should execute exactly once — successful tasks are not re-enqueued
        tasks.Should().AllSatisfy(t => t.ExecuteCount.Should().Be(1));
    }

    [Fact]
    public async Task LifetimeTermination_StopsProcessing()
    {
        var lifetime = new Lifetime();
        _balancer.Run(lifetime);

        await Task.Delay(50);
        lifetime.Terminate();

        var task = new FakeTask("late", delay: TimeSpan.Zero);
        _queue.Enqueue(task);

        await Task.Delay(100);

        task.ExecuteCount.Should()
            .Be(0,
                "tasks enqueued after lifetime termination should not execute");
    }

    private static ITaskBalancerConfig CreateConfig(TaskBalancerOptions options)
    {
        var config = Substitute.For<ITaskBalancerConfig>();
        config.Value.Returns(options);
        return config;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        condition().Should().BeTrue("timed out waiting for condition");
    }
}

public class OrderTrackingTask : IPriorityTask
{
    public OrderTrackingTask(string id, TaskPriority priority, List<string> executionOrder)
    {
        Id = id;
        Priority = priority;
        _executionOrder = executionOrder;
    }

    private readonly List<string> _executionOrder;

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay => TimeSpan.Zero;

    public Task Execute()
    {
        lock (_executionOrder)
        {
            _executionOrder.Add(Id);
        }

        return Task.CompletedTask;
    }
}