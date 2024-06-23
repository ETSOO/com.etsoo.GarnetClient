using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace com.etsoo.GarnetClient
{
    /// <summary>
    /// Garnet cache service collection extensions
    /// Garnet 缓存服务集合扩展
    /// </summary>
    public static class GarnetCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Add Garnet cache
        /// 添加 Garnet 缓存
        /// </summary>
        /// <param name="services">Services</param>
        /// <param name="setupAction">Setup action</param>
        /// <returns>Collection</returns>
        public static IServiceCollection AddGarnetCache(this IServiceCollection services, Action<GarnetCacheOptions> setupAction)
        {
            // Inject the GarnetCacheOptions
            services.Configure(setupAction);

            // Inject the IConnectionMultiplexer
            // Means the IConnectionMultiplexer is ready to use in your code if you need
            services.AddSingleton(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GarnetCacheOptions>>().Value;

                var lazyConnection = new Lazy<IConnectionMultiplexer>(() =>
                {
                    IConnectionMultiplexer connection;
                    if (options.ConnectionMultiplexerFactory is null)
                    {
                        connection = ConnectionMultiplexer.Connect(options.GetConfiguredOptions());
                    }
                    else
                    {
                        connection = options.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
                    }

                    // Setup
                    if (options.ProfilingSession is not null)
                    {
                        connection.RegisterProfiler(options.ProfilingSession);
                    }

                    // Add library name suffix
                    // Share the same thing with the ASP.NET Core AddStackExchangeRedisCache
                    connection.AddLibraryNameSuffix("aspnet");
                    connection.AddLibraryNameSuffix("DC");

                    return connection;
                });

                return lazyConnection.Value;
            });

            // Add the distributed cache service
            services.AddTransient<IDistributedCache, GarnetCache>();

            return services;
        }
    }
}