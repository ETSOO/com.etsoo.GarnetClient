using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace com.etsoo.GarnetClient
{
    /// <summary>
    /// Garnet cache options
    /// Garnet 缓存选项
    /// </summary>
    public class GarnetCacheOptions
    {
        /// <summary>
        /// The configuration used to connect to Garnet, like a simple connection string "localhost:6379"
        /// 链接 Garnet 使用的配置，如简单的链接字符串 "localhost:6379"
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// The configuration used to connect to Garnet, is preferred over Configuration
        /// 用于链接 Garnet 的配置，优先于 Configuration，配置此选项，忽略 Configuration
        /// </summary>
        public ConfigurationOptions? ConfigurationOptions { get; set; }

        /// <summary>
        /// Gets or sets a delegate to create the ConnectionMultiplexer instance
        /// 用于创建 ConnectionMultiplexer 实例的委托
        /// </summary>
        public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; set; }

        /// <summary>
        /// The Garnet instance name. Allows partitioning a single backend cache for use with multiple apps/services.
        /// If set, the cache keys are prefixed with this value.
        /// 缓存实例名称。允许将单个后端缓存分区用于多个应用/服务，如果设置，缓存键将以此值为前缀。
        /// </summary>
        public string? InstanceName { get; set; }

        /// <summary>
        /// The Garnet profiling session
        /// Garnet 诊断会话
        /// </summary>
        public Func<ProfilingSession>? ProfilingSession { get; set; }

        internal ConfigurationOptions GetConfiguredOptions()
        {
            var options = ConfigurationOptions ?? ConfigurationOptions.Parse(Configuration!);

            // we don't want an initially unavailable server to prevent DI creating the service itself
            options.AbortOnConnectFail = false;

            return options;
        }
    }
}
