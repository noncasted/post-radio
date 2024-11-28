namespace Internal
{
    public class ViewableDelegate : EventSource, IViewableDelegate
    {
   
    }

    public class ViewableDelegate<T> : EventSource<T>, IViewableDelegate<T>
    {

    }

    public class ViewableDelegate<T1, T2> : EventSource<T1, T2>, IViewableDelegate<T1, T2>
    {

    }
}