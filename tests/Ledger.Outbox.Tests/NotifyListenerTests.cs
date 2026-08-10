using System.Diagnostics;
using Ledger.OutboxDispatcher;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ledger.Outbox.Tests;

[Collection(nameof(DispatcherCollection))]
public sealed class NotifyListenerTests(DispatcherFixture fixture)
{
    /// <summary>Se o LISTEN nao funcionasse, este teste so passaria esperando os 5s do polling.</summary>
    private static readonly TimeSpan MUCH_SHORTER_THAN_POLL_INTERVAL = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Appending_An_Event_Wakes_The_Dispatcher_Without_Waiting_For_The_Poll()
    {
        await fixture.ResetAsync();

        var wake = new OutboxWakeSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new NotifyListenerService(fixture.DataSource, wake, NullLogger<NotifyListenerService>.Instance);

        await listener.StartAsync(cts.Token);
        await WaitForListenerAsync();

        var stopwatch = Stopwatch.StartNew();
        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        var woke = await wake.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
        stopwatch.Stop();

        await listener.StopAsync(CancellationToken.None);

        Assert.True(woke, "o dispatcher nao foi acordado pelo NOTIFY");
        Assert.True(stopwatch.Elapsed < MUCH_SHORTER_THAN_POLL_INTERVAL,
            $"acordou em {stopwatch.Elapsed.TotalSeconds:F2}s — lento demais para ter vindo do NOTIFY");
    }

    [Fact]
    public async Task Wake_Signal_Times_Out_When_Nothing_Is_Appended()
    {
        await fixture.ResetAsync();

        var wake = new OutboxWakeSignal();

        var woke = await wake.WaitAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None);

        Assert.False(woke);
    }

    /// <summary>
    /// Coalescencia: N sinais sem leitor no meio viram 1 token, nao N.
    /// Sem banco de proposito — a propriedade e do canal, e testa-la via NOTIFY seria racy
    /// (notificacoes que chegam DEPOIS da primeira leitura acordam de novo, e isso e correto).
    /// </summary>
    [Fact]
    public async Task Ten_Signals_Without_A_Reader_Collapse_Into_One()
    {
        var wake = new OutboxWakeSignal();

        for (var i = 0; i < 10; i++)
        {
            wake.Signal();
        }

        Assert.True(await wake.WaitAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None));
        Assert.False(await wake.WaitAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None));
    }

    /// <summary>
    /// A propriedade que a coalescencia precisa preservar: descartar sinais nao pode
    /// descartar trabalho. Um unico wake tem que levar o dispatcher a drenar a rajada inteira.
    /// </summary>
    [Fact]
    public async Task A_Single_Wake_Drains_The_Whole_Burst()
    {
        await fixture.ResetAsync();
        var topic = $"ledger.burst.{Guid.NewGuid():N}";

        var wake = new OutboxWakeSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new NotifyListenerService(fixture.DataSource, wake, NullLogger<NotifyListenerService>.Instance);

        await listener.StartAsync(cts.Token);
        await WaitForListenerAsync();

        const int BURST = 10;
        for (var i = 0; i < BURST; i++)
        {
            await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");
        }

        Assert.True(await wake.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));

        var dispatcher = fixture.CreateDispatcher(topic);
        while (await dispatcher.RunOnceAsync(cts.Token)) { }

        await listener.StopAsync(CancellationToken.None);

        Assert.Equal(BURST, await fixture.CountByStateAsync("sent"));
        Assert.Equal(0, await fixture.CountByStateAsync("pending"));
    }

    [Fact]
    public async Task Dispatcher_Publishes_Promptly_When_Driven_By_Notify()
    {
        await fixture.ResetAsync();
        var topic = $"ledger.notify.{Guid.NewGuid():N}";

        var wake = new OutboxWakeSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new NotifyListenerService(fixture.DataSource, wake, NullLogger<NotifyListenerService>.Instance);

        await listener.StartAsync(cts.Token);
        await WaitForListenerAsync();

        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", """{"amountCents":42}""");

        Assert.True(await wake.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));
        await fixture.CreateDispatcher(topic).RunOnceAsync(cts.Token);

        await listener.StopAsync(CancellationToken.None);

        Assert.Equal(1, await fixture.CountByStateAsync("sent"));
    }

    /// <summary>Da tempo do LISTEN estar registrado antes de gerar o NOTIFY.</summary>
    private static Task WaitForListenerAsync() => Task.Delay(TimeSpan.FromSeconds(1));
}
