using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Lazywait;

public static partial class Awaitable
{
    /// <summary>
    /// Returns a <see cref="Task"/> that completes when the property selected by
    /// <paramref name="selector"/> on <paramref name="owner"/> becomes equal to
    /// <paramref name="expected"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current value is checked synchronously before subscribing — if it
    /// already equals <paramref name="expected"/>, a completed task is returned
    /// and no event subscription is made. Otherwise, the method subscribes to
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> and completes the
    /// first time the property is observed to equal <paramref name="expected"/>.
    /// </para>
    /// <para>
    /// Equality uses <see cref="EqualityComparer{T}.Default"/>. The selector
    /// must be a simple property access expression (e.g. <c>x =&gt; x.MyProp</c>);
    /// anything more complex throws <see cref="ArgumentException"/>.
    /// </para>
    /// <para>
    /// Per the INPC convention, a <see cref="PropertyChangedEventArgs.PropertyName"/>
    /// that is <c>null</c> or empty is treated as "any property changed" and will
    /// trigger a re-check.
    /// </para>
    /// <para>
    /// The event handler and any cancellation registrations are released once the
    /// returned task transitions to a terminal state.
    /// </para>
    /// </remarks>
    /// <typeparam name="TOwner">
    /// The type that owns the property. Must implement
    /// <see cref="INotifyPropertyChanged"/>.
    /// </typeparam>
    /// <typeparam name="TValue">The property's value type.</typeparam>
    /// <param name="owner">The instance whose property is observed.</param>
    /// <param name="selector">
    /// A property-access expression identifying the property to observe, e.g.
    /// <c>x =&gt; x.CurrentPosition</c>.
    /// </param>
    /// <param name="expected">The value to wait for.</param>
    /// <param name="timeout">
    /// Optional timeout. If the property does not reach <paramref name="expected"/>
    /// within this duration, the returned task faults with
    /// <see cref="TimeoutException"/>. When <c>null</c> the wait is unbounded.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the wait. On cancellation the returned task transitions to the
    /// canceled state.
    /// </param>
    /// <returns>
    /// A task that completes when the property equals <paramref name="expected"/>,
    /// faults with <see cref="TimeoutException"/> on timeout, or is canceled via
    /// <paramref name="cancellationToken"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="owner"/> or <paramref name="selector"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="selector"/> is not a simple property access expression.
    /// </exception>
    /// <example>
    /// <code>
    /// await device.WaitUntil(x => x.CurrentPosition, 2, TimeSpan.FromSeconds(5));
    /// </code>
    /// </example>
    public static Task WaitUntil<TOwner, TValue>(
        this TOwner owner,
        Expression<Func<TOwner, TValue>> selector,
        TValue expected,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TOwner : INotifyPropertyChanged
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (selector is null) throw new ArgumentNullException(nameof(selector));

        var propertyName = GetPropertyName(selector);
        var getter = selector.Compile();

        if (EqualityComparer<TValue>.Default.Equals(getter(owner), expected))
            return Task.CompletedTask;

        var waiter = new PropertyWaiter<TOwner, TValue>(owner, getter, propertyName, expected);
        return waiter.Start(timeout, cancellationToken);
    }

    private static string GetPropertyName<TOwner, TValue>(Expression<Func<TOwner, TValue>> selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression unary) body = unary.Operand;
        if (body is MemberExpression { Member: PropertyInfo } member) return member.Member.Name;

        throw new ArgumentException(
            "Selector must be a simple property access expression, e.g. x => x.MyProperty.",
            nameof(selector));
    }
}
