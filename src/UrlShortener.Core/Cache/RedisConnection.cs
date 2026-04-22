using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace UrlShortener.Core.Cache;

// Thin wrapper around the StackExchange.Redis connection.
// Ventem.Core.Redis wraps this same library — we're writing the wrapper
// by hand so you see what it does.
//
// IMPORTANT: registered as a SINGLETON in DI. The StackExchange.Redis
// ConnectionMultiplexer is thread-safe and expensive to create, so the
// whole app shares ONE instance. Same pattern in api-member.
public sealed class RedisConnection : IDisposable
{
    public IConnectionMultiplexer Multiplexer { get; }
    public IDatabase Db => Multiplexer.GetDatabase();

    public RedisConnection(IConfiguration config)
    {
        var cs = config.GetConnectionString("Redis")
                 ?? throw new InvalidOperationException("ConnectionStrings:Redis missing");
        Multiplexer = ConnectionMultiplexer.Connect(cs);
    }

    public IServer GetServer()
    {
        // For IServer-level commands (like KEYS pattern scan) we need to
        // pick a specific endpoint. With a single-node local Redis, the
        // first endpoint is the only one.
        var endpoint = Multiplexer.GetEndPoints().First();
        return Multiplexer.GetServer(endpoint);
    }

    public void Dispose() => Multiplexer.Dispose();
}
