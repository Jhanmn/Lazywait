using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Lazywait;

internal sealed class PropertyWaiter<TOwner, TValue>(
    TOwner owner,
    Func<TOwner, TValue> getter,
    string propertyName,
    TValue expected)
    where TOwner : INotifyPropertyChanged
{
    private static readonly EqualityComparer<TValue> Comparer = EqualityComparer<TValue>.Default;

    private readonly TOwner _owner = owner;
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Start(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        _owner.PropertyChanged += OnPropertyChanged;

        if (Comparer.Equals(getter(_owner), expected))
        {
            _owner.PropertyChanged -= OnPropertyChanged;
            return Task.CompletedTask;
        }

        var registrations = new List<IDisposable>();
        if (cancellationToken.CanBeCanceled)
            registrations.Add(cancellationToken.Register(() => _tcs.TrySetCanceled(cancellationToken)));

        CancellationTokenSource? timeoutCts = null;
        if (timeout.HasValue)
        {
            timeoutCts = new CancellationTokenSource(timeout.Value);
            registrations.Add(timeoutCts.Token.Register(() =>
                _tcs.TrySetException(new TimeoutException(
                    $"Timed out after {timeout.Value} waiting for {typeof(TOwner).Name}.{propertyName} to equal {expected}."))));
        }

        _tcs.Task.ContinueWith(_ =>
        {
            _owner.PropertyChanged -= OnPropertyChanged;
            foreach (var r in registrations) r.Dispose();
            timeoutCts?.Dispose();
        }, TaskScheduler.Default);

        return _tcs.Task;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != propertyName && !string.IsNullOrEmpty(e.PropertyName)) return;
        try
        {
            if (Comparer.Equals(getter(_owner), expected))
                _tcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }
}