using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Ledger.Idempotency;

public sealed class IdempotencyMiddleware : IMiddleware
{
    private const string HEADER_NAME = "Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly NpgsqlDataSource _dataSource;

    public IdempotencyMiddleware(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!IsWrite(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!TryGetKey(context, out var key))
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest, "Idempotency-Key header required");
            return;
        }

        var requestHash = await ComputeRequestHashAsync(context);

        await using var conn = await _dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var tx = await conn.BeginTransactionAsync(context.RequestAborted);

        await AdvisoryLock.AcquireForKeyAsync(conn, tx, key, context.RequestAborted);

        var existing = await IdempotencyStore.TryGetAsync(conn, tx, key, context.RequestAborted);
        if (existing is not null)
        {
            await tx.CommitAsync(context.RequestAborted);
            await HandleCachedAsync(context, existing, requestHash);
            return;
        }

        await ProcessAndCacheAsync(context, next, conn, tx, key, requestHash);
    }

    private static bool TryGetKey(HttpContext context, out string key)
    {
        key = string.Empty;
        if (!context.Request.Headers.TryGetValue(HEADER_NAME, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            return false;

        key = values.ToString();
        
        return true;
    }

    private static async Task<string> ComputeRequestHashAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        
        context.Request.Body.Position = 0;
        
        return CanonicalJson.Sha256Hash(string.IsNullOrWhiteSpace(body) ? "null" : body);
    }

    private static async Task HandleCachedAsync(HttpContext context, IdempotencyRecord existing, string requestHash)
    {
        if (existing.RequestHash != requestHash)
        {
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity,
                "Idempotency-Key reused with different payload");
            return;
        }

        context.Response.StatusCode = existing.ResponseStatus;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(existing.ResponseBody, context.RequestAborted);
    }

    private static async Task ProcessAndCacheAsync(HttpContext context, RequestDelegate next, NpgsqlConnection conn, NpgsqlTransaction tx, string key, string requestHash)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            var responseText = await new StreamReader(buffer).ReadToEndAsync();
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                await IdempotencyStore.InsertAsync(conn, tx, new IdempotencyRecord
                {
                    Key = key,
                    RequestHash = requestHash,
                    ResponseStatus = context.Response.StatusCode,
                    ResponseBody = string.IsNullOrWhiteSpace(responseText) ? "null" : responseText,
                    ExpiresAt = DateTime.UtcNow.Add(Ttl)
                }, context.RequestAborted);
                await tx.CommitAsync(context.RequestAborted);
            }
            else
            {
                await tx.RollbackAsync(context.RequestAborted);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsWrite(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method);

    private static async Task WriteProblem(HttpContext ctx, int status, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = JsonSerializer.Serialize(new { title = "Idempotency violation", status, detail });
        await ctx.Response.WriteAsync(problem, ctx.RequestAborted);
    }
}
