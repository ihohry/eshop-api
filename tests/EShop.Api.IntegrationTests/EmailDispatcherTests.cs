using EShop.Domain.Email;
using EShop.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.Api.IntegrationTests;

public sealed class EmailDispatcherTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Dispatched_message_is_sent_by_the_worker()
    {
        var ct = TestContext.Current.CancellationToken;
        var sender = new RecordingEmailSender();
        var dispatcher = new BackgroundEmailDispatcher();
        using var worker = CreateWorker(dispatcher, sender);
        await worker.StartAsync(ct);

        await dispatcher.DispatchAsync(Message("Hello"), ct);
        var sent = await sender.NextSentAsync(ct);
        await worker.StopAsync(ct);

        Assert.Equal("Hello", sent.Subject);
    }

    [Fact]
    public async Task Dispatch_returns_while_the_sender_is_still_busy()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new RecordingEmailSender { FirstSendGate = gate.Task };
        var dispatcher = new BackgroundEmailDispatcher();
        using var worker = CreateWorker(dispatcher, sender);
        await worker.StartAsync(ct);

        var dispatch = dispatcher.DispatchAsync(Message("Slow"), ct).AsTask();
        await sender.FirstSendStarted;

        Assert.True(dispatch.IsCompletedSuccessfully);
        Assert.False(sender.HasSentMessage);

        gate.SetResult();
        await sender.NextSentAsync(ct);
        await worker.StopAsync(ct);
    }

    [Fact]
    public async Task Failed_send_does_not_stop_the_worker()
    {
        var ct = TestContext.Current.CancellationToken;
        var sender = new RecordingEmailSender { FailSubject = "Fails" };
        var dispatcher = new BackgroundEmailDispatcher();
        using var worker = CreateWorker(dispatcher, sender);
        await worker.StartAsync(ct);

        await dispatcher.DispatchAsync(Message("Fails"), ct);
        await dispatcher.DispatchAsync(Message("Works"), ct);
        var sent = await sender.NextSentAsync(ct);
        await worker.StopAsync(ct);

        Assert.Equal("Works", sent.Subject);
    }

    [Fact]
    public async Task Stopping_the_worker_sends_the_messages_that_are_still_waiting()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new RecordingEmailSender { FirstSendGate = gate.Task };
        var dispatcher = new BackgroundEmailDispatcher();
        using var worker = CreateWorker(dispatcher, sender);
        await worker.StartAsync(ct);

        await dispatcher.DispatchAsync(Message("One"), ct);
        await dispatcher.DispatchAsync(Message("Two"), ct);
        await dispatcher.DispatchAsync(Message("Three"), ct);
        await sender.FirstSendStarted;

        var stopping = worker.StopAsync(ct);
        gate.SetResult();
        await stopping.WaitAsync(WaitLimit, ct);

        Assert.Equal("One", (await sender.NextSentAsync(ct)).Subject);
        Assert.Equal("Two", (await sender.NextSentAsync(ct)).Subject);
        Assert.Equal("Three", (await sender.NextSentAsync(ct)).Subject);
    }

    private static EmailSendingWorker CreateWorker(BackgroundEmailDispatcher dispatcher, IEmailSender sender) =>
        new(dispatcher, sender, NullLogger<EmailSendingWorker>.Instance);

    private static EmailMessage Message(string subject) =>
        new("customer@example.com", subject, "Body");
}