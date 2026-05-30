using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lazywait.Tests.Stubs;

internal class TestClass : INotifyPropertyChanged
{
    private int _currentPosition;
    private bool _isActive;
    private string? _name;

    public int CurrentPosition
    {
        get => _currentPosition;
        set => Set(ref _currentPosition, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    public string? Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public bool ShouldThrow { get; set; }
    public int ThrowingProperty => ShouldThrow ? throw new InvalidOperationException("boom") : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RaiseAnyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void RaiseEmptyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public void RaiseUnrelatedChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Unrelated"));

    public void RaiseForThrowingProperty() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThrowingProperty)));

    public int SubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}