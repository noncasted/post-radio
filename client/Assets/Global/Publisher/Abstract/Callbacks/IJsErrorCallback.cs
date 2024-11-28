using System;

namespace Global.Publisher
{
    public interface IJsErrorCallback
    {
        event Action<string> Exception; 
    }
}