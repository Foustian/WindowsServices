using System.Collections.Generic;

namespace IQMedia.Service.Common.Threading
{
    public class ThreadSafeList<T>
    {
        private readonly object _lock = new object();
        private List<T> _list;

        public ThreadSafeList()
        {
            _list = new List<T>();
        }
        public ThreadSafeList(int capacity)
        {
            _list = new List<T>(capacity);
        }
        
        public void Add(T item)
        {
            lock(_lock)
            {
                _list.Add(item);
            }
        }

        public bool Remove(T item)
        {
            lock(_lock)
            {
                return _list.Remove(item);
            }
        }

        public bool Contains(T item)
        {
            lock(_lock)
            {
                return _list.Contains(item);
            }
        }
    }
}
