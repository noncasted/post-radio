namespace Benchmarks;

public static class TestAssert
{
    public static void Equal<T>(T expected, T actual, string context = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.Equal failed: expected {expected}, got {actual}"
                : $"[{context}] Assert.Equal failed: expected {expected}, got {actual}";

            throw new TestAssertException(message);
        }
    }

    public static void NotEqual<T>(T unexpected, T actual, string context = "")
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.NotEqual failed: value should not be {actual}"
                : $"[{context}] Assert.NotEqual failed: value should not be {actual}";

            throw new TestAssertException(message);
        }
    }

    public static void True(bool condition, string message = "Assert.True failed")
    {
        if (!condition)
            throw new TestAssertException(message);
    }

    public static void False(bool condition, string message = "Assert.False failed")
    {
        if (condition)
            throw new TestAssertException(message);
    }

    public static void GreaterThan<T>(T value, T threshold, string context = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) <= 0)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.GreaterThan failed: {value} is not greater than {threshold}"
                : $"[{context}] Assert.GreaterThan failed: {value} is not greater than {threshold}";

            throw new TestAssertException(message);
        }
    }

    public static void LessThan<T>(T value, T threshold, string context = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) >= 0)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.LessThan failed: {value} is not less than {threshold}"
                : $"[{context}] Assert.LessThan failed: {value} is not less than {threshold}";

            throw new TestAssertException(message);
        }
    }

    public static void Throws<TException>(Action action, string context = "")
        where TException : Exception
    {
        var threw = false;

        try
        {
            action();
        }
        catch (TException)
        {
            threw = true;
        }

        if (!threw)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.Throws failed: expected {typeof(TException).Name} but none was thrown"
                : $"[{context}] Assert.Throws failed: expected {typeof(TException).Name} but none was thrown";

            throw new TestAssertException(message);
        }
    }

    public static async Task ThrowsAsync<TException>(Func<Task> action, string context = "")
        where TException : Exception
    {
        var threw = false;

        try
        {
            await action();
        }
        catch (TException)
        {
            threw = true;
        }

        if (!threw)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.ThrowsAsync failed: expected {typeof(TException).Name} but none was thrown"
                : $"[{context}] Assert.ThrowsAsync failed: expected {typeof(TException).Name} but none was thrown";

            throw new TestAssertException(message);
        }
    }

    public static void Contains<T>(T item, IEnumerable<T> collection, string context = "")
    {
        if (!collection.Contains(item))
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Assert.Contains failed: {item} not found in collection"
                : $"[{context}] Assert.Contains failed: {item} not found in collection";

            throw new TestAssertException(message);
        }
    }

    public static void NotNull<T>(T? value, string context = "") where T : class
    {
        if (value == null)
        {
            var message = string.IsNullOrEmpty(context)
                ? "Assert.NotNull failed: value is null"
                : $"[{context}] Assert.NotNull failed: value is null";

            throw new TestAssertException(message);
        }
    }
}

public class TestAssertException : Exception
{
    public TestAssertException(string message) : base(message)
    {
    }
}