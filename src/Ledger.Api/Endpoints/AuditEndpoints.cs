using Ledger.EventStore.Interfaces;

namespace Ledger.Api.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit/events", async (long? cursor, int? pageSize, string? type, IEventStore store, CancellationToken ct) =>
        {
            var from = cursor ?? -1;
            var max = Math.Min(pageSize ?? 100, 500);
            var events = await store.ReadAllAsync(from, max, ct);
            var filtered = type is null ? events : events.Where(e => e.Type == type).ToList();
            var next = filtered.Count == 0 ? (long?)null : filtered[^1].Sequence;
            return Results.Ok(new { events = filtered, nextCursor = next });
        });
    }
}
