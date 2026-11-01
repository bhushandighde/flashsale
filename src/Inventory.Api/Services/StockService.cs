using StackExchange.Redis;

namespace Inventory.Api.Services;

public enum ReserveResult { Rejected = 0, Reserved = 1, AlreadyReserved = 2 }

public class StockService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(2);

    private static string StockKey(string sku) => $"stock:{sku}";
    private static string ReservationKey(Guid orderId) => $"reservation:{orderId}";

    // KEYS[1] = stock key, KEYS[2] = reservation key
    // ARGV[1] = quantity, ARGV[2] = ttl seconds
    // Check + decrement + reservation hold happen as ONE atomic step.
    private const string ReserveScript = @"
        if redis.call('EXISTS', KEYS[2]) == 1 then
            return 2
        end
        local stock = tonumber(redis.call('GET', KEYS[1]) or '0')
        local qty = tonumber(ARGV[1])
        if stock < qty then
            return 0
        end
        redis.call('DECRBY', KEYS[1], qty)
        redis.call('SET', KEYS[2], qty, 'NX', 'EX', tonumber(ARGV[2]))
        return 1";

    // Only returns stock if the reservation still exists, so calling it twice is safe.
    private const string ReleaseScript = @"
        local qty = redis.call('GET', KEYS[2])
        if not qty then
            return 0
        end
        redis.call('INCRBY', KEYS[1], tonumber(qty))
        redis.call('DEL', KEYS[2])
        return 1";

    public async Task SeedAsync(string sku, int quantity) =>
        await _db.StringSetAsync(StockKey(sku), quantity);

    public async Task<long> GetStockAsync(string sku)
    {
        var value = await _db.StringGetAsync(StockKey(sku));
        return value.HasValue ? (long)value : 0;
    }

    public async Task<ReserveResult> ReserveAsync(Guid orderId, string sku, int quantity)
    {
        var result = await _db.ScriptEvaluateAsync(
            ReserveScript,
            [StockKey(sku), ReservationKey(orderId)],
            [quantity, (int)ReservationTtl.TotalSeconds]);
        return (ReserveResult)(int)result;
    }

    public async Task<bool> ReleaseAsync(Guid orderId, string sku)
    {
        var result = await _db.ScriptEvaluateAsync(
            ReleaseScript,
            [StockKey(sku), ReservationKey(orderId)]);
        return (int)result == 1;
    }
    public async Task<bool> ReserveNaiveAsync(string sku, int quantity)
    {
        var stock = await GetStockAsync(sku);                        // 1. read
        if (stock < quantity) return false;                          // 2. check
        await _db.StringSetAsync(StockKey(sku), stock - quantity);   // 3. write
        return true;
    }
}

