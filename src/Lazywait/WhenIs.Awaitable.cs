using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Lazywait;

public static partial class Awaitable
{
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

        TValue Getter(TOwner to) => (TValue)property.GetValue(owner);
    }
}

