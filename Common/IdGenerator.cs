using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Common
{
    public class IdGenerator<T> where T : IConvertible
    {
        private long _counter = 0;
        private readonly ConcurrentQueue<T> _freeIds = new ConcurrentQueue<T>();

        public void Free(T id)
        {
            _freeIds.Enqueue(id);
        }

        public T Get()
        {
            if (_freeIds.TryDequeue(out T oValue))
                return oValue;

            var value = (T)Convert.ChangeType(Interlocked.Add(ref _counter, 1),typeof(T));
            return value;
        }
    }

    public class IdCounter
    {
        private long _counter = 0;
        private long _resetValue;

        public IdCounter(uint resetValue)
        {
            _resetValue = resetValue;
        }

        public uint Get()
        {
            var value = Interlocked.Add(ref _counter, 1);
            if (value >= _resetValue)
                _counter = 0;
            return (uint)value;
        }
    }
}
