namespace Benchmarks;

public interface IConcurrentIterationTestPayload
{
    int Iterations { get; set; }
    int Concurrent { get; set; }
}