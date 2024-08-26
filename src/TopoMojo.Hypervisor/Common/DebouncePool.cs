using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace TopoMojo.Hypervisor.Common
{
    public sealed class DebouncePool<T>
    {
        private string _id;
        private DateTimeOffsetRange _currentDebounce = null;
        private readonly object _currentDebounceLock = new object();
        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1);
        private readonly ConcurrentBag<T> _items = new ConcurrentBag<T>();

        public DebouncePool()
        {
            DebouncePeriod = 0;
        }

        public DebouncePool(int debounceDurationMs)
        {
            DebouncePeriod = debounceDurationMs;
        }

        public DebouncePool(int debounceDurationMs, int maxTotalDebounceDurationMs)
        {
            DebouncePeriod = debounceDurationMs;
            MaxTotalDebounce = maxTotalDebounceDurationMs;
        }

        public int DebouncePeriod { get; set; }
        public int? MaxTotalDebounce { get; set; } = null;

        public Task<DebouncePoolBatch<T>> Add(T item, CancellationToken cancellationToken)
            => AddRange(new T[] { item }, cancellationToken);

        public async Task<DebouncePoolBatch<T>> AddRange(IEnumerable<T> items, CancellationToken cancellationToken)
        {
            // add the item to the collection immediately (independent of debounce settings)
            foreach (var item in items.ToArray())
            {
                _items.Add(item);
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
                        End = nowish.AddMilliseconds(this.DebouncePeriod)
                    };

                    _id = Guid.NewGuid().ToString();
                }
                else
                {
                    // if there's a current debounce happening, refresh the period length (e.g. if the debounce period is 300ms and an item is added 250ms after the last one,
                    // the debounce timer should reset to 300ms after the second item is added)
                    _currentDebounce.End = nowish.AddMilliseconds(this.DebouncePeriod);

                    // BUT if there's a maximum total debounce time, we have to ensure that we don't overflow it, so clamp the value to the maximum remaining if it would
                    if (this.MaxTotalDebounce.HasValue)
                    {
                        var maxDebounceEnds = _currentDebounce.Start.AddMilliseconds(this.MaxTotalDebounce.Value);
                        if (maxDebounceEnds < _currentDebounce.End)
                        {
                            _currentDebounce.End = maxDebounceEnds;
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
                    int delayLength;
                    do
                    {

                        // lock on the current debounce when we read its `.End`, because
                        // another thread could be writing it (above)
                        lock (_currentDebounceLock)
                        {
                            delayLength = (int)Math.Ceiling((_currentDebounce.End - DateTimeOffset.UtcNow).TotalMilliseconds);
                        }

                        if (delayLength > 0)
                            await Task.Delay(delayLength, cancellationToken);
                    }
                    while (delayLength > 0);
                }

                var itemsThreadSafe = Array.Empty<T>();

                lock (_currentDebounceLock)
                {
                    _currentDebounce = null;

                    // get a new array that points to the contents of _items for thread safety
                    itemsThreadSafe = this._items.ToArray();

                    // clear the collection (.Clear() isn't supported in .netstandard2.0)
                    foreach (var item in _items)
                    {
                        _items.TryTake(out _);
                    }
                }

                return new DebouncePoolBatch<T>
                {
                    Id = _id,
                    Items = itemsThreadSafe
                };
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }
    }
}
