using System.Text.Json;
using Dapper;
using Ledger.Domain.Events;

namespace Ledger.Projections.Tests;

public class BalanceInvariantTests : IClassFixture<ProjectionsFixture>
{
    private readonly ProjectionsFixture _fixture;

    public BalanceInvariantTests(ProjectionsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(42, 5, 30)]
    [InlineData(1337, 3, 20)]
    [InlineData(7, 8, 50)]
    public async Task Balance_Equals_Sum_Of_Postings_For_Every_Account(int seed, int accountCount, int transactionCount)
    {
        var accounts = Enumerable.Range(0, accountCount).Select(_ => Guid.NewGuid()).ToArray();
        var rng = new Random(seed);
        var expected = new Dictionary<Guid, long>();

        for (int i = 0; i < transactionCount; i++)
        {
            var debitAccount = accounts[rng.Next(accountCount)];
            Guid creditAccount;
            do { creditAccount = accounts[rng.Next(accountCount)]; } while (creditAccount == debitAccount);

            var amount = rng.Next(100, 100_000);
            expected[debitAccount] = expected.GetValueOrDefault(debitAccount) + amount;
            expected[creditAccount] = expected.GetValueOrDefault(creditAccount) - amount;

            var evt = new TransactionPosted
            {
                TransactionId = Guid.NewGuid(),
                Description = $"tx-{i}",
                OccurredAt = DateTime.UtcNow,
                Postings = new[]
                {
                    new PostingSnapshot(debitAccount, amount, null, "BRL"),
                    new PostingSnapshot(creditAccount, null, amount, "BRL")
                }
            };

            await _fixture.Store.AppendAsync(evt.TransactionId, expectedVersion: -1, [evt]);
        }

        bool hasMore;
        do { hasMore = await _fixture.Runner.RunOnceAsync(CancellationToken.None); } while (hasMore);

        await using var conn = await _fixture.DataSource.OpenConnectionAsync();
        foreach (var (accountId, expectedBalance) in expected)
        {
            var actual = await conn.QuerySingleOrDefaultAsync<long?>(
                "SELECT balance_cents FROM balances WHERE account_id = @id AND currency = 'BRL'",
                new { id = accountId });

            Assert.Equal(expectedBalance, actual ?? 0L);
        }

        await conn.ExecuteAsync("TRUNCATE events, balances, statement, projection_checkpoints RESTART IDENTITY");
    }
}
