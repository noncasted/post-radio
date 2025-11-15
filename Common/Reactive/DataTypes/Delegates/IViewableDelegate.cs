namespace Common
{
    public interface IViewableDelegate : IEventSource
    {
    }
    
    public interface IViewableDelegate<T> : IEventSource<T>
    {
    }
    
    public interface IViewableDelegate<T1, T2> : IEventSource<T1, T2>
    {
    }
}