using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace com.etsoo.GarnetClient
{
    /// <summary>
    /// Garnet cache (^1.0.14 64 bit)
    /// https://microsoft.github.io/garnet/
    /// Garnet 缓存，1.0.14 以上 64 位
    /// </summary>
    public class GarnetCache : IDistributedCache
    {
        private const string SlidingExpirationKey = "sldexp";
        private const string DataKey = "data";

        private static TimeSpan? GetExpirationTimeSpan(DistributedCacheEntryOptions options)
        {
            if (options.SlidingExpiration.HasValue)
            {
                return options.SlidingExpiration;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                return options.AbsoluteExpirationRelativeToNow;
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                return options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
            }
            else
            {
                return null;
            }
        }

        private readonly IConnectionMultiplexer _connection;
        private readonly GarnetCacheOptions _options;
        private readonly RedisKey _prefix;

        /// <summary>
        /// Constructor
        /// 构造函数
        /// </summary>
        /// <param name="connection">StackExchange.Redis ConnectionMultiplexer</param>
        /// <param name="options">Options</param>
        public GarnetCache(IConnectionMultiplexer connection, IOptions<GarnetCacheOptions> options)
        {
            _connection = connection;
            _options = options.Value;

            if (!string.IsNullOrEmpty(_options.InstanceName))
            {
                _prefix = new RedisKey(_options.InstanceName);
            }
        }

        public byte[]? Get(string key)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            var hashFields = db.HashGet(prefixedKey, [DataKey, SlidingExpirationKey]);

            DoRefresh(db, prefixedKey, hashFields[1]);

            return hashFields[0];
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            var hashFields = await db.HashGetAsync(prefixedKey, [DataKey, SlidingExpirationKey]);

            await DoRefreshAsync(db, prefixedKey, hashFields[1]);

            return hashFields[0];
        }

        private void DoRefresh(IDatabase db, RedisKey key, RedisValue redisValue)
        {
            if (!redisValue.HasValue) return;

            var ticks = ((long?)redisValue).GetValueOrDefault();
            if (ticks <= 0) return;

            var ts = TimeSpan.FromTicks(ticks);
            db.KeyExpire(key, ts);
        }

        private async ValueTask DoRefreshAsync(IDatabase db, RedisKey key, RedisValue redisValue)
        {
            if (!redisValue.HasValue) return;

            var ticks = ((long?)redisValue).GetValueOrDefault();
            if (ticks <= 0) return;

            var ts = TimeSpan.FromTicks(ticks);
            await db.KeyExpireAsync(key, ts);
        }

        public void Refresh(string key)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            var hashFields = db.HashGet(prefixedKey, [SlidingExpirationKey]);
            DoRefresh(db, prefixedKey, hashFields[0]);
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            var hashFields = await db.HashGetAsync(prefixedKey, [SlidingExpirationKey]);
            await DoRefreshAsync(db, prefixedKey, hashFields[0]);
        }

        public void Remove(string key)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();
            db.KeyDelete(prefixedKey);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();
            return db.KeyDeleteAsync(prefixedKey);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            HashEntry[] hashEntries = options.SlidingExpiration.HasValue
                ? [new HashEntry(DataKey, value), new HashEntry(SlidingExpirationKey, options.SlidingExpiration.Value.Ticks)]
                : [new HashEntry(DataKey, value)]
            ;

            var timeSpan = GetExpirationTimeSpan(options);
            if (timeSpan is null)
            {
                db.HashSet(prefixedKey, hashEntries);
            }
            else
            {
                db.WaitAll(
                    db.HashSetAsync(prefixedKey, hashEntries),
                    db.KeyExpireAsync(prefixedKey, timeSpan)
                );
            }
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            var prefixedKey = _prefix.Append(key);
            var db = _connection.GetDatabase();

            HashEntry[] hashEntries = options.SlidingExpiration.HasValue
                ? [new HashEntry(DataKey, value), new HashEntry(SlidingExpirationKey, options.SlidingExpiration.Value.Ticks)]
                : [new HashEntry(DataKey, value)]
            ;

            var timeSpan = GetExpirationTimeSpan(options);
            if (timeSpan is null)
            {
                await db.HashSetAsync(prefixedKey, hashEntries);
            }
            else
            {
                await Task.Run(() =>
                {
                    db.WaitAll(
                        db.HashSetAsync(prefixedKey, hashEntries),
                        db.KeyExpireAsync(prefixedKey, timeSpan)
                    );
                }, token);
            }
        }
    }
}
