using System;
using System.Threading;

namespace Internal
{
    public class TerminatedLifetime : ILifetime
    {
        public CancellationToken Token => CancellationToken.None;
        public bool IsTerminated => true;
        
        public void Listen(Action callback)
        {
        }

        public void RemoveListener(Action callback)
        {
            
        }

        public void RemoveTerminationListener(Action callback)
        {
        }

        public ILifetime CreateChild()
        {
            return new TerminatedLifetime();
        }

        public void Terminate()
        {
        }
    }
}