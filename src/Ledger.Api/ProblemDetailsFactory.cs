namespace Ledger.Api;

public static class ProblemDetailsFactory
{
    public static object Create(int status, string title, string detail, string? type = null) => new
    {
        type = type ?? $"https://httpstatuses.io/{status}",
        title,
        status,
        detail
    };
}
