# NuExt.Minimal.Mvvm.SourceGenerator

`NuExt.Minimal.Mvvm.SourceGenerator` is a Roslyn source generator for the lightweight MVVM framework [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm). It emits boilerplate for properties, commands, validation, and localization **at compile time**, so you can focus on app logic.

[![NuGet](https://img.shields.io/nuget/v/NuExt.Minimal.Mvvm.SourceGenerator.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.SourceGenerator)
[![Build](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator?label=license)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.Minimal.Mvvm.SourceGenerator.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.SourceGenerator)

## Requirements

- **.NET Standard 2.0+, .NET 8+, .NET Framework 4.6.2+**
- **Language**: C# 12+
- Works with: `NuExt.Minimal.Mvvm` (core) and optional UI integrations (`…Wpf`, `…MahApps.Metro`).

## Features

- **Properties & commands**
  - Generates properties (change notification) from backing fields / partial properties
  - Generates command properties from methods (sync/async)
  - Emits cached event args for zero‑alloc hot paths
- **Validation (`INotifyDataErrorInfo`)**
  - Thread‑safe store (per‑scope lists in `ConcurrentDictionary`)
  - **Copy‑on‑write** updates + CAS; safe for UI enumeration
  - **Scopes**: property‑level and entity‑level (entity is **`null` or `""`** across all APIs)
  - Helpers: `HasErrorsFor(scope)`, `GetErrors(scope)`, `GetErrorsSnapshot()`
- **WPF command requery**
  - `[UseCommandManager]` wires generated command to `CommandManager.RequerySuggested`
- **Localization**
  - `[Localize]` populates a static class from a JSON file (provided via `AdditionalFiles`)
- **Custom attributes**
  - `[AlsoNotify]` to raise extra `PropertyChanged`
  - `[CustomAttribute]` to apply an attribute to a generated member

## Attributes (summary)

- **`Minimal.Mvvm.NotifyAttribute`**: Generates a property (from a field or partial property) or a command property (from a method).  
  Options: `PropertyName`, `CallbackName`, `PreferCallbackWithParameter`, `Getter`, `Setter`.
- **`Minimal.Mvvm.AlsoNotifyAttribute`**: Notifies additional properties when the annotated property changes.
- **`Minimal.Mvvm.CustomAttributeAttribute`**: Specifies a fully qualified attribute name to be applied to a generated property.
- **`Minimal.Mvvm.UseCommandManagerAttribute`**: Enables automatic `CanExecute` reevaluation for the generated command property (WPF only).
- **`Minimal.Mvvm.NotifyDataErrorInfoAttribute`**: Generates validation infrastructure for `INotifyDataErrorInfo`.
- **`Minimal.Mvvm.LocalizeAttribute`**: Localizes the target class using the provided JSON file (MSBuild `AdditionalFiles`).

## Install

```bash
dotnet add package NuExt.Minimal.Mvvm.SourceGenerator
# and one of:
dotnet add package NuExt.Minimal.Mvvm
# or
dotnet add package NuExt.Minimal.Mvvm.Wpf
# or
dotnet add package NuExt.Minimal.Mvvm.MahApps.Metro
```

## Quick Start

```csharp
using Minimal.Mvvm;
using System.Threading.Tasks;

public partial class PersonModel : BindableBase
{
    [Notify, AlsoNotify(nameof(FullName))]
    private string? _name;

    [Notify, AlsoNotify(nameof(FullName))]
    private string? _surname;

    public string FullName => $"{_surname} {_name}";

    // Generates IAsyncCommand<string?>? ShowInfoCommand
    [Notify("ShowInfoCommand"), UseCommandManager]
    [CustomAttribute("System.Text.Json.Serialization.JsonIgnore")]
    private async Task ShowInfoAsync(string? text)
    {
        await Task.Delay(100);
    }
}
```

**What you get (conceptually)**:
- `Name/Surname` properties with `PropertyChanged` and `FullName` notifications.
- `IAsyncCommand<string?>? ShowInfoCommand` property with WPF requery wiring (via `[UseCommandManager]`).
- Cached PropertyChangedEventArgs etc. for zero‑alloc notifications.

## Validation semantics (concise)

- **Scopes**: `null`/`""` = **entity‑level**; `"PropertyName"` = property‑level.
- **Clearing**:
  - `ClearErrors(null)` or `ClearErrors("")` → clears **entity‑level only**.
  - `ClearErrors(nameof(Property))` → clears that property only.
  - `ClearAllErrors()` → full reset (raises `ErrorsChanged` per affected scope + updates `HasErrors`).
- **Updates**: `SetError`, `SetErrors` (merge), `ReplaceErrors` (replace), `RemoveError`.
  - Copy‑on‑write lists + CAS; early‑return on no‑op (no redundant `ErrorsChanged`).

> **Threading**: notifications are marshaled to UI via a lazily captured `SynchronizationContext`.

### Async validation helpers (`SetValidationTask` / `CancelValidationTask`)

When a class is annotated with `[NotifyDataErrorInfo]`, the generator exposes helpers to manage **asynchronous** per‑property validation tasks:

- `SetValidationTask(Task task, CancellationTokenSource cts, string propertyName)`  
  Associates the task with `propertyName`, **cancelling and disposing** any previous task for that scope (lock‑free CAS under the hood).
- `CancelValidationTask(string propertyName = null)`  
  Cancels/disposes the tracked task for `propertyName`; with `null` cancels **all** tasks.
- `CancelAllValidationTasks()`  
  Convenience wrapper to cancel every tracked task (use in teardown).
- `HasErrorsFor(scope)` / `SetError` / `SetErrors` / `ReplaceErrors` / `RemoveError`  
  Same semantics as in the synchronous flow (entity = `null`/`""`).

**Usage pattern (per property):**
```csharp
[NotifyDataErrorInfo]
public partial class LoginViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserName))
        {
            // Cancel previous async validation for this property
            CancelValidationTask(nameof(UserName));

            // Run sync validation first (one notification, via ReplaceErrors)
            ValidateUserNameSync();

            // Start async validation with a linked CTS
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var task = ValidateUserNameAsync(UserName, cts.Token);

            // Observe faults to avoid UnobservedTaskException (no UI marshal)
            task.ContinueWith(
                t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | 
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // Publish/track the task for this property
            SetValidationTask(task, cts, nameof(UserName));
        }
    }

    private async Task ValidateUserNameAsync(string userName, CancellationToken ct)
    {
        try
        {
            // Debounce
            await Task.Delay(250, ct);

            // If sync errors still exist — skip async
            if (HasErrorsFor(nameof(UserName)))
                return;

            // Ensure value is still current
            if (!string.Equals(UserName, userName, StringComparison.Ordinal))
                return;

            // Do the actual async check (no UI context capture)
            var locked = await _auth.IsUserLockedAsync(userName, ct).ConfigureAwait(false);

            if (locked) SetError("This user is locked.", nameof(UserName));
            else        ClearErrors(nameof(UserName));
        }
        catch (OperationCanceledException) { /* ignore */ }
    }

    // Teardown: cancel all pending validations
    protected override async Task UninitializeAsyncCore(CancellationToken ct)
    {
        CancelAllValidationTasks();
        await base.UninitializeAsyncCore(ct);
    }
}
```
#### Notes

- Always **cancel** the previous task for the same property before starting a new one: `CancelValidationTask(nameof(Property))`.
- Prefer a **linked** `CancellationTokenSource` tied to the VM lifetime.
- Mark faults as **observed** (via `ContinueWith(OnlyOnFaulted|ExecuteSynchronously)`) or catch inside the async validator.
- Use `ConfigureAwait(false)` inside validators; the generator **marshals notifications to the UI** via a lazily captured `SynchronizationContext`.
- Keep `CanExecute`/UI logic driven by **property‑level** errors (e.g., `HasErrorsFor(nameof(UserName))`), while *entity‑level* banners are cleared at the **start** of a new attempt (`ClearErrors("")`).

### WPF notes (displaying errors)

- Field‑level errors bind with `Validation.HasError` and delayed index access to `(Validation.Errors)[0]` (avoid warnings).
- Entity‑level errors can be shown by a hidden binding host (bind to VM with `ValidatesOnNotifyDataErrors=True`, read its `Validation.Errors`).

See the [WpfAppSample](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/tree/main/samples/WpfAppSample) in the repository for a minimal, production‑style pattern (entity banner + field messages).

## UseCommandManagerAttribute

When applied to a command field or method, enables automatic `CanExecute` reevaluation for the generated command property by subscribing to the WPF `CommandManager.RequerySuggested` event. This attribute is used together with `[Notify]` for commands that should react to global UI state changes.

```csharp
using Minimal.Mvvm;

public partial class MyViewModel : ViewModelBase
{
    [Notify, UseCommandManager]
    private IRelayCommand? _saveCommand;
}
```

## Localization (`AdditionalFiles`)

Example `csproj` snippet for `[Localize("local.en.json")]`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Resources\local.en.json" />
</ItemGroup>
```

The generator will create a static class with string properties populated from that JSON. At runtime, localization can also be loaded from the specified file (see [samples](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/tree/main/samples)).

## Ecosystem

- [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm)
- [NuExt.Minimal.Behaviors.Wpf](https://github.com/nu-ext/NuExt.Minimal.Behaviors.Wpf)
- [NuExt.Minimal.Mvvm.Wpf](https://github.com/nu-ext/NuExt.Minimal.Mvvm.Wpf)
- [NuExt.Minimal.Mvvm.MahApps.Metro](https://github.com/nu-ext/NuExt.Minimal.Mvvm.MahApps.Metro)
- [NuExt.System](https://github.com/nu-ext/NuExt.System)
- [NuExt.System.Data](https://github.com/nu-ext/NuExt.System.Data)
- [NuExt.System.Data.SQLite](https://github.com/nu-ext/NuExt.System.Data.SQLite)

## Contributing

Issues and PRs are welcome. Keep changes minimal and performance-conscious.

## License

MIT. See LICENSE.