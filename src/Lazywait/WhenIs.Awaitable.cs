using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lazywait;

public static partial class Awaitable
{
    /// <summary>
    /// Asynchronously waits until the specified property on an
    /// <see cref="INotifyPropertyChanged"/> owner equals the given value.
    /// </summary>
    /// <typeparam name="TOwner">The type of the object that raises property change notifications.</typeparam>
    /// <typeparam name="TValue">The type of the property value being observed.</typeparam>
    /// <param name="owner">The object whose property is observed for the target value.</param>
    /// <param name="observableName">The name of the property to observe on <paramref name="owner"/>.</param>
    /// <param name="value">The value to wait for the property to equal.</param>
    /// <param name="timeout">
    /// An optional maximum time to wait. If <see langword="null"/>, the wait does not time out.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of the wait operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the observed property equals <paramref name="value"/>.
    /// </returns>
    /// <exception cref="TimeoutException">
    /// Thrown if <paramref name="timeout"/> elapses before the property reaches <paramref name="value"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if <paramref name="cancellationToken"/> is canceled before the property reaches <paramref name="value"/>.
    /// </exception>
    public static Task WhenIs<TOwner, TValue>(
        this TOwner owner, 
        string observableName, 
        TValue value,
        TimeSpan? timeout = null,
        CancellationToken  cancellationToken = default) 
        where TOwner : INotifyPropertyChanged
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(observableName)) throw new ArgumentException(nameof(observableName),  $"{nameof(observableName)} cannot be empty");

        var property = typeof(TOwner).GetProperty(observableName)
                       ?? throw new ArgumentException($"No property '{observableName}' on {typeof(TOwner).Name}.", nameof(observableName));

        var propertyWaiter = new PropertyWaiter<TOwner, TValue>(owner, Getter, observableName, value);
        return propertyWaiter.Start(timeout, cancellationToken);

        TValue Getter(TOwner to)
        {
            try
            {
                return (TValue)property.GetValue(owner);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Surface the getter's own exception, matching the compiled-delegate
                // behavior of WaitUntil rather than reflection's wrapper.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
        }
    }
}

