using System.Linq.Expressions;
using System.Reflection;
using Lazywait.Tests.Stubs;

namespace Lazywait.Tests;

/// <summary>
/// Behavioural contract shared by every "wait for an INPC property to reach a
/// value" entry point. Both <see cref="Awaitable.WaitUntil{TOwner,TValue}"/> and
/// <see cref="Awaitable.WhenIs{TOwner,TValue}"/> funnel into the same
/// <c>PropertyWaiter</c>, so the observable behaviour must be identical.
/// </summary>
/// <remarks>
/// Concrete fixtures implement <see cref="Wait{TValue}"/> to invoke the specific
/// API under test. NUnit runs every inherited test once per concrete fixture.
/// API-specific argument validation lives in those subclasses.
/// </remarks>
internal abstract class AwaitableContractTests
{
    /// <summary>
    /// Starts a wait for <paramref name="selector"/> on <paramref name="owner"/>
    /// to equal <paramref name="expected"/>, using the API under test.
    /// </summary>
    protected abstract Task Wait<TValue>(
        TestClass owner,
        Expression<Func<TestClass, TValue>> selector,
        TValue expected,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    [Test]
    public async Task Completes_when_property_reaches_value()
    {
        var device = new TestClass();

        var waiter = Wait(device, x => x.CurrentPosition, 2, TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            device.CurrentPosition = 1;
            await Task.Delay(50);
            device.CurrentPosition = 2;
        });

        await waiter;
    }

    [Test]
    public async Task Completes_immediately_when_already_equal()
    {
        var device = new TestClass { CurrentPosition = 7 };
        var task = Wait(device, x => x.CurrentPosition, 7, TimeSpan.FromSeconds(1));

        Assert.That(task.IsCompletedSuccessfully, Is.True);
        await task;
        Assert.That(device.SubscriberCount, Is.Zero, "no subscription should be left when value already matched");
    }

    [Test]
    public void Throws_TimeoutException_when_value_never_reached()
    {
        var device = new TestClass();
        Assert.ThrowsAsync<TimeoutException>(async () =>
            await Wait(device, x => x.CurrentPosition, 42, TimeSpan.FromMilliseconds(100)));
    }

    [Test]
    public async Task Works_for_bool_property()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.IsActive, true, TimeSpan.FromSeconds(5));
        _ = Task.Run(async () => { await Task.Delay(50); device.IsActive = true; });
        await waiter;
    }

    [Test]
    public async Task Works_for_reference_type_property()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.Name, "ready", TimeSpan.FromSeconds(5));
        _ = Task.Run(async () => { await Task.Delay(50); device.Name = "ready"; });
        await waiter;
    }

    [Test]
    public async Task Works_when_expected_value_is_null()
    {
        var device = new TestClass { Name = "something" };
        var waiter = Wait(device, x => x.Name, (string?)null, TimeSpan.FromSeconds(5));
        _ = Task.Run(async () => { await Task.Delay(50); device.Name = null; });
        await waiter;
    }

    [Test]
    public void Cancellation_cancels_the_wait()
    {
        var device = new TestClass();
        using var cts = new CancellationTokenSource();

        var waiter = Wait(device, x => x.CurrentPosition, 1, timeout: null, cts.Token);

        cts.Cancel();

        var ex = Assert.ThrowsAsync<TaskCanceledException>(async () => await waiter);
        Assert.That(ex!.CancellationToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public async Task Cancellation_after_completion_is_ignored()
    {
        var device = new TestClass();
        using var cts = new CancellationTokenSource();

        var waiter = Wait(device, x => x.CurrentPosition, 5, TimeSpan.FromSeconds(5), cts.Token);
        device.CurrentPosition = 5;
        await waiter;

        cts.Cancel();
        Assert.That(waiter.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public void Pre_cancelled_token_cancels_immediately()
    {
        var device = new TestClass();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await Wait(device, x => x.CurrentPosition, 1, timeout: null, cts.Token));
    }

    [Test]
    public async Task Null_PropertyName_triggers_recheck()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 3, TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            device.CurrentPosition = 3;
            device.RaiseAnyChanged();
        });

        await waiter;
    }

    [Test]
    public async Task Empty_PropertyName_triggers_recheck()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 3, TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            device.CurrentPosition = 3;
            device.RaiseEmptyChanged();
        });

        await waiter;
    }

    [Test]
    public void Unrelated_property_changes_do_not_complete_wait()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 99, TimeSpan.FromMilliseconds(200));

        device.IsActive = true;
        device.Name = "anything";
        device.RaiseUnrelatedChanged();

        Assert.ThrowsAsync<TimeoutException>(async () => await waiter);
    }

    [Test]
    public async Task Unbounded_wait_with_null_timeout_completes()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 4, timeout: null);

        _ = Task.Run(async () => { await Task.Delay(50); device.CurrentPosition = 4; });

        await waiter;
    }

    [Test]
    public void Throwing_getter_faults_the_task()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.ThrowingProperty, 1, TimeSpan.FromSeconds(5));

        device.ShouldThrow = true;
        device.RaiseForThrowingProperty();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await waiter);
        Assert.That(ex!.Message, Is.EqualTo("boom"));
    }

    [Test]
    public async Task Handler_is_unsubscribed_after_success()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 1, TimeSpan.FromSeconds(5));
        device.CurrentPosition = 1;
        await waiter;

        await WaitForAsync(() => device.SubscriberCount == 0, TimeSpan.FromSeconds(2));
        Assert.That(device.SubscriberCount, Is.Zero);
    }

    [Test]
    public async Task Handler_is_unsubscribed_after_timeout()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 1, TimeSpan.FromMilliseconds(100));

        Assert.ThrowsAsync<TimeoutException>(async () => await waiter);

        await WaitForAsync(() => device.SubscriberCount == 0, TimeSpan.FromSeconds(2));
        Assert.That(device.SubscriberCount, Is.Zero);
    }

    [Test]
    public async Task Handler_is_unsubscribed_after_cancellation()
    {
        var device = new TestClass();
        using var cts = new CancellationTokenSource();
        var waiter = Wait(device, x => x.CurrentPosition, 1, timeout: null, cts.Token);

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await waiter);

        await WaitForAsync(() => device.SubscriberCount == 0, TimeSpan.FromSeconds(2));
        Assert.That(device.SubscriberCount, Is.Zero);
    }

    [Test]
    public async Task Changes_after_completion_do_not_affect_task()
    {
        var device = new TestClass();
        var waiter = Wait(device, x => x.CurrentPosition, 1, TimeSpan.FromSeconds(5));
        device.CurrentPosition = 1;
        await waiter;

        device.CurrentPosition = 2;
        device.CurrentPosition = 1;
        device.RaiseAnyChanged();

        Assert.That(waiter.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Concurrent_waiters_for_different_values_each_complete()
    {
        var device = new TestClass();
        var w1 = Wait(device, x => x.CurrentPosition, 1, TimeSpan.FromSeconds(5));
        var w2 = Wait(device, x => x.CurrentPosition, 2, TimeSpan.FromSeconds(5));

        device.CurrentPosition = 1;
        await w1;
        Assert.That(w2.IsCompleted, Is.False);

        device.CurrentPosition = 2;
        await w2;
    }

    [Test]
    public async Task Concurrent_waiters_for_different_properties_each_complete()
    {
        var device = new TestClass();
        var posWaiter = Wait(device, x => x.CurrentPosition, 5, TimeSpan.FromSeconds(5));
        var activeWaiter = Wait(device, x => x.IsActive, true, TimeSpan.FromSeconds(5));

        device.IsActive = true;
        await activeWaiter;
        Assert.That(posWaiter.IsCompleted, Is.False);

        device.CurrentPosition = 5;
        await posWaiter;
    }

    private protected static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Extracts the property name from a simple property-access selector, for
    /// fixtures whose API takes a property name rather than an expression.
    /// </summary>
    private protected static string PropertyNameOf<TValue>(Expression<Func<TestClass, TValue>> selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression unary) body = unary.Operand;
        if (body is MemberExpression { Member: PropertyInfo } member) return member.Member.Name;

        throw new ArgumentException("Selector must be a simple property access expression.", nameof(selector));
    }
}
