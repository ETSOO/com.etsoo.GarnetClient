using com.etsoo.GarnetClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace GarnetClientTests
{
    [TestClass]
    public class AddGarnetCacheTests
    {
        const string configuration = "localhost:6379, abortConnect=false";

        private readonly IConnectionMultiplexer connection;
        private readonly IDistributedCache cache;

        public AddGarnetCacheTests()
        {
            var services = new ServiceCollection();
            services.AddGarnetCache(options =>
            {
                options.Configuration = configuration;
                options.InstanceName = "Test";
            });

            var provider = services.BuildServiceProvider();
            cache = provider.GetRequiredService<IDistributedCache>();
            connection = provider.GetRequiredService<IConnectionMultiplexer>();
        }

        [TestMethod]
        public void TestSetup()
        {
            Assert.IsTrue(connection.IsConnected);

            var prefix = new RedisKey("Test");
            var key = prefix.Append("Hello");
            Assert.AreEqual("TestHello", key.ToString());
        }

        [TestMethod]
        public async Task TestStringFeatures()
        {
            var db = connection.GetDatabase();

            var key = "TestHello";
            var ts = TimeSpan.FromSeconds(0.2);
            var value = "World";

            // String
            var result = await db.StringSetAsync(key, value, ts);
            Assert.IsTrue(result);

            var savedValue = await db.StringGetAsync(key);
            Assert.AreEqual(value, (string?)savedValue);

            var deleteResult = await db.KeyDeleteAsync(key);
            Assert.IsTrue(deleteResult);
        }

        [TestMethod]
        public async Task TestBytesFeatures()
        {
            var db = connection.GetDatabase();

            var key = "TestBytes";
            var ts = TimeSpan.FromSeconds(0.2);
            byte[] value = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

            // String
            var result = await db.StringSetAsync(key, value, ts);
            Assert.IsTrue(result);

            var savedValue = await db.StringGetAsync(key);
            CollectionAssert.AreEqual(value, (byte[]?)savedValue);

            var deleteResult = await db.KeyDeleteAsync(key);
            Assert.IsTrue(deleteResult);
        }

        [TestMethod]
        public async Task TestHashFeatures()
        {
            var db = connection.GetDatabase();

            var key = "TestHash";
            var ts = TimeSpan.FromSeconds(0.2);

            // Hash
            await db.HashSetAsync(key, [new HashEntry("data", "Garnet"), new HashEntry("sldexp", ts.Ticks)]);
            await db.KeyExpireAsync(key, ts);

            var hashValue = await db.HashGetAllAsync(key);
            Assert.AreEqual(2, hashValue.Length);
            Assert.AreEqual("Garnet", (string?)hashValue[0].Value);

            var hashFields = await db.HashGetAsync(key, ["data", "sldexp"]);
            Assert.AreEqual(2, hashFields.Length);
            Assert.AreEqual("Garnet", (string?)hashFields[0]);

            await Task.Delay(210);
            var keyExists = await db.KeyExistsAsync(key);
            Assert.IsFalse(keyExists);
        }

        [TestMethod]
        public async Task TestHashEmptyValues()
        {
            var db = connection.GetDatabase();

            var key = "TestHashEmpty";
            var ts = TimeSpan.FromSeconds(0.2);

            // Hash
            await db.HashSetAsync(key, [new HashEntry("data", "Garnet")]);
            await db.KeyExpireAsync(key, ts);

            var hashFields = await db.HashGetAsync(key, ["data", "sldexp"]);
            Assert.AreEqual(2, hashFields.Length);
            Assert.AreEqual("Garnet", (string?)hashFields[0]);
            Assert.IsFalse(hashFields[1].HasValue);

            var sldexp = (long?)hashFields[1];
            Assert.IsNull(sldexp);

            await Task.Delay(210);
            var keyExists = await db.KeyExistsAsync(key);
            Assert.IsFalse(keyExists);
        }

        [TestMethod]
        public async Task TestHashNullValues()
        {
            var db = connection.GetDatabase();

            var key = "TestHashNull";
            TimeSpan? ts = null;

            // Hash
            await db.HashSetAsync(key, [new HashEntry("data", "Garnet"), new HashEntry("sldexp", ts?.Ticks ?? 0)]);
            await db.KeyExpireAsync(key, ts);

            var hashFields = await db.HashGetAsync(key, ["data", "sldexp"]);
            Assert.AreEqual(2, hashFields.Length);
            Assert.IsTrue(hashFields[1].HasValue);

            var deleteResult = await db.KeyDeleteAsync(key);
            Assert.IsTrue(deleteResult);
        }

        [TestMethod]
        public async Task TestCache()
        {
            var key = "TestCache";
            var value = "Hello, world!";
            var ts = TimeSpan.FromSeconds(0.2);

            await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { SlidingExpiration = ts });

            await Task.Delay(100);
            var savedValue = await cache.GetStringAsync(key);
            Assert.AreEqual(value, savedValue);

            await Task.Delay(101);
            savedValue = await cache.GetStringAsync(key);
            Assert.AreEqual(value, savedValue);

            await Task.Delay(201);
            savedValue = await cache.GetStringAsync(key);
            Assert.IsNull(savedValue);
        }
    }
}