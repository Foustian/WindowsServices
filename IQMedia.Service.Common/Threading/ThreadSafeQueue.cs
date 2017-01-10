using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using IQMedia.Service.Common.Util;

namespace IQMedia.Service.Common.Threading
{
    public class ThreadSafeQueue<T> : Queue<T>
    {
        public T DequeueOrWait()
        {
            Monitor.Enter(this);
            try
            {
                while(Count == 0)
                    //Wait to reaquire the lock since the quere is empty
                    Monitor.Wait(this);
                T val = base.Dequeue();
                //Since we waited, we're guarenteed a value, no need to check, just dequeue
                Monitor.PulseAll(this);
                return val;
            }
            catch(Exception ex)
            {
                Logger.Error("ThreadSafeQueue .::. Dequeue Error", ex);
                return default(T);
            }
            finally
            {
                //Finally trumps return so this code block will always execute...
                Monitor.Exit(this);
            }
        }

        public new T Dequeue()
        {
            Monitor.Enter(this);
            T val = base.Dequeue();
            Monitor.PulseAll(this);
            Monitor.Exit(this);
            return val;
        }

        public new void Enqueue(T obj)
        {
            Monitor.Enter(this);
            try
            {
                base.Enqueue(obj);
                Monitor.PulseAll(this);
            }
            catch(Exception ex)
            {
                Logger.Error("ThreadSafeQueue .::. Enqueue Error", ex);
            }
            finally
            {
                Monitor.Exit(this);
            }
        }
    }
}
