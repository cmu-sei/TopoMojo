using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace TopoMojo.Hypervisor.Common
{
    public sealed class DebounceAddCollection<T>
    {
        private readonly object _collectionLock = new object();
        private DateTimeOffsetRange _currentDebounce = null;
        private readonly object _currentDebounceLock = new object();
        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1);
        private readonly double _debouncePeriod;
        private readonly int? _maxTotalDebounce = null;
        private readonly ConcurrentBag<T> _items = new ConcurrentBag<T>();
        public event EventHandler<IEnumerable<T>> Modified;

        public DebounceAddCollection(int debounceDurationMs)
        {
            _debouncePeriod = debounceDurationMs;
        }

        public DebounceAddCollection(int debounceDurationMs, int maxTotalDebounceDurationMs)
        {
            _debouncePeriod = debounceDurationMs;
            _maxTotalDebounce = maxTotalDebounceDurationMs;
        }

        public int Length { get => _items.Count; }

        public Task<IEnumerable<T>> Add(T item, CancellationToken cancellationToken)
            => AddRange(new T[] { item }, cancellationToken);

        public async Task<IEnumerable<T>> AddRange(IEnumerable<T> items, CancellationToken cancellationToken)
        {
            // add the item to the collection immediately (independent of debounce settings)
            lock (_collectionLock)
            {
                foreach (var item in items.ToArray())
                {
                    _items.Add(item);
                }
            }

            var nowish = DateTimeOffset.UtcNow;
            lock (_currentDebounceLock)
            {
                if (_currentDebounce is null)
                {
                    // start a new debounce if there's not one currently in the hopper
                    _currentDebounce = new DateTimeOffsetRange
                    {
                        Start = nowish,
                        End = nowish.AddMilliseconds(_debouncePeriod)
                    };
                }
                else
                {
                    // if there's a current debounce happening, refresh the period length (e.g. if the debounce period is 300ms and an item is added 250ms after the last one,
                    // the debounce timer should reset to 300ms after the second item is added)
                    _currentDebounce.End = nowish.AddMilliseconds(_debouncePeriod);
                    // BUT if there's a maximum total debounce time, we have to ensure that we don't overflow it, so clamp the value to the maximum remaining if it would
                    if (_maxTotalDebounce.HasValue)
                    {
                        var maxDebounceEnds = _currentDebounce.Start.AddMilliseconds(_maxTotalDebounce.Value);
                        if (maxDebounceEnds < _currentDebounce.End)
                        {
                            _currentDebounce.End = maxDebounceEnds;
                            Console.WriteLine($"Clamped to {_currentDebounce.End}");
                        }
                    }
                }
            }

            // after waiting for the appropriate delay, return the contents of the collection
            try
            {
                await _semaphoreLock.WaitAsync(cancellationToken);
                if (_currentDebounce != null)
                {
                    var delayLength = 0;
                    do
                    {
                        delayLength = (int)Math.Ceiling((_currentDebounce.End - DateTimeOffset.UtcNow).TotalMilliseconds);
                        if (delayLength > 0)
                            await Task.Delay(delayLength, cancellationToken);
                    }
                    while (delayLength > 0);
                }

                lock (_currentDebounceLock)
                {
                    _currentDebounce = null;
                }

                // get a new array that points to the contents of _items for thread safety
                var itemsThreadSafe = this._items.ToArray();
                this.Modified?.Invoke(this, itemsThreadSafe);
                return itemsThreadSafe;
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }

        public void Clear()
        {
            foreach (var item in _items)
            {
                _items.TryTake(out _);
            }
        }
    }
}
