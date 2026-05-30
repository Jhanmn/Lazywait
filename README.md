<p align="center">
  <img src="resources/lazywaiter_icon.svg" alt="Lazywait" width="160" />
</p>

# Lazywait

Lightweight, allocation-conscious extension methods for `await`-ing changes on
[`INotifyPropertyChanged`](https://learn.microsoft.com/dotnet/api/system.componentmodel.inotifypropertychanged)
objects. Instead of wiring up `PropertyChanged` handlers by hand, you write a
single line and `await` until a property reaches the value you care about — with
optional timeout and cancellation support.

```csharp
// Wait until the device finishes moving, or fail after 5 seconds.
await device.WaitUntil(x => x.CurrentPosition, target: 2, timeout: TimeSpan.FromSeconds(5));
```

## Features

- **Strongly-typed, expression-based API** — `WaitUntil(x => x.Prop, value)`
  gives you compile-time safety and refactor-friendly property references.
- **String-based API** — `WhenIs("Prop", value)` for cases where the property
  name is only known at runtime.
- **Synchronous fast path** — if the property already equals the expected value,
  a completed task is returned and no event subscription is made.
- **Timeout support** — faults with `TimeoutException` if the value isn't reached
  in time.
- **Cancellation support** — pass a `CancellationToken` to abandon the wait.
- **Automatic cleanup** — event handlers and registrations are released as soon
  as the task reaches a terminal state.
- **`netstandard2.0`** — works across .NET Framework, .NET Core, and modern .NET.

## Installation

Add the project to your solution, or reference the compiled assembly:

```bash
dotnet add reference path/to/src/Lazywait/Lazywait.csproj
```

## Usage

Both methods are extension methods on any type implementing
`INotifyPropertyChanged`. Add the namespace:

```csharp
using Lazywait;
```

### `WaitUntil` — expression-based

```csharp
// Wait indefinitely.
await viewModel.WaitUntil(x => x.IsLoading, false);

// Wait with a timeout.
await viewModel.WaitUntil(x => x.Status, Status.Ready, TimeSpan.FromSeconds(10));

// Wait with cancellation.
await viewModel.WaitUntil(x => x.Progress, 100, cancellationToken: token);
```

The selector must be a simple property-access expression (e.g. `x => x.MyProp`);
anything more complex throws `ArgumentException`.

### `WhenIs` — string-based

```csharp
await viewModel.WhenIs(nameof(ViewModelBase.Status), Status.Ready, TimeSpan.FromSeconds(10));
```

Useful when the property name isn't known at compile time. Throws
`ArgumentException` if no matching property exists on the type.

## Behavior notes

- Equality is compared with `EqualityComparer<TValue>.Default`.
- Per the INPC convention, a `PropertyChanged` event with a `null` or empty
  `PropertyName` is treated as "any property changed" and triggers a re-check.
- On timeout the returned task faults with `TimeoutException`; on cancellation it
  transitions to the canceled state.

## API reference

```csharp
Task WaitUntil<TOwner, TValue>(
    this TOwner owner,
    Expression<Func<TOwner, TValue>> selector,
    TValue expected,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default)
    where TOwner : INotifyPropertyChanged;

Task WhenIs<TOwner, TValue>(
    this TOwner owner,
    string observableName,
    TValue value,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default)
    where TOwner : INotifyPropertyChanged;
```
