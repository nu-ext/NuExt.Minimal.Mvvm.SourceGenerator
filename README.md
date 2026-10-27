# NuExt.Minimal.Mvvm.SourceGenerator

`NuExt.Minimal.Mvvm.SourceGenerator` is a Roslyn source generator for the lightweight MVVM framework [NuExt.Minimal.Mvvm](https://github.com/nu-ext/NuExt.Minimal.Mvvm). It emits boilerplate for properties, commands, validation, and localization **at compile time**, so you can focus on app logic.

[![NuGet](https://img.shields.io/nuget/v/NuExt.Minimal.Mvvm.SourceGenerator.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.SourceGenerator)
[![Ask DeepWiki](https://img.shields.io/badge/Ask-DeepWiki-blue)](https://deepwiki.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator)
[![Build](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/actions/workflows/ci.yml/badge.svg)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator?label=license)](https://github.com/nu-ext/NuExt.Minimal.Mvvm.SourceGenerator/blob/main/LICENSE)
[![Downloads](https://img.shields.io/nuget/dt/NuExt.Minimal.Mvvm.SourceGenerator.svg)](https://www.nuget.org/packages/NuExt.Minimal.Mvvm.SourceGenerator)

## Requirements

- **Consumer target frameworks:** .NET Standard 2.0/2.1, .NET 8/9/10, and .NET Framework 4.6.2+
- **Language:** C# 12+
- **Core package:** `NuExt.Minimal.Mvvm` or a compatible UI integration package

## Features

- **Properties**
  - Generates observable properties from backing fields and partial properties.
  - Emits cached event arguments for allocation-free notification paths.
  - Initializes generated backing fields for non-nullable partial `string` properties with `string.Empty` in nullable-enabled code.
  - Supports related-property notifications through `[AlsoNotify]`.
  - Supports attributes on generated members through `[CustomAttribute]`.
- **Commands**
  - Generates command properties from synchronous and asynchronous methods.
  - Supports complete command generation through `[GenerateCommands]` and `[Command]`.
  - Generates command creation, initialization, lookup, enumeration, and cleanup overrides.
  - Discovers `CanExecute` callbacks in the current type and its accessible base types.
  - Supports explicit command names, `CanExecute` callback names, command-property accessibility, and WPF `CommandManager` integration.
- **Validation (`INotifyDataErrorInfo`)**
  - Uses thread-safe, copy-on-write error storage.
  - Supports property-level and entity-level validation scopes.
  - Provides synchronous and asynchronous validation helpers.
- **Localization**
  - Generates localization properties from JSON files supplied through MSBuild `AdditionalFiles`.

## Attributes

- **`[GenerateCommands]`**: Enables complete command generation for a partial class derived from a compatible command ViewModel base class.
- **`[Command]`**: Generates a command property and the code that creates its command instance from the annotated method, and includes the command in the generated lifecycle infrastructure.
  - **Options:** `PropertyName`, `CanExecute`, `Getter`, `Setter`.
- **`[Notify]`**: Generates an observable property from a field or partial property. When applied to a method, it provides command-property-only generation.
  - **Options:** `PropertyName`, `CallbackName`, `Getter`, `Setter`.
- **`[AlsoNotify]`**: Raises notifications for additional properties when the generated property changes.
- **`[CustomAttribute]`**: Applies an attribute to a generated member.
- **`[UseCommandManager]`**: Enables automatic `CanExecute` reevaluation through WPF `CommandManager.RequerySuggested`. Can be applied together with `[Command]` for complete command generation or with `[Notify]` for command-property-only generation.
- **`[NotifyDataErrorInfo]`**: Generates `INotifyDataErrorInfo` infrastructure.
- **`[Localize]`**: Generates localization members from a JSON file supplied through MSBuild `AdditionalFiles`.

## Install

```shell
dotnet add package NuExt.Minimal.Mvvm.SourceGenerator
```

Install the core package or a compatible UI integration package:

```shell
dotnet add package NuExt.Minimal.Mvvm
```

```shell
dotnet add package NuExt.Minimal.Mvvm.Wpf
```

```shell
dotnet add package NuExt.Minimal.Mvvm.MahApps.Metro
```

## Quick Start

```csharp
using Minimal.Mvvm;
using Minimal.Mvvm.Wpf;

[GenerateCommands]
public partial class PersonViewModel : CommandViewModelBase
{
    [Notify, AlsoNotify(nameof(FullName))]
    private string? _name;

    [Notify, AlsoNotify(nameof(FullName))]
    private string? _surname;

    public string FullName => $"{_surname} {_name}";

    private bool CanShowInfo(string? text)
    {
        return !string.IsNullOrWhiteSpace(text);
    }

    [Command]
    private async Task ShowInfoAsync(string? text)
    {
        await Task.Delay(100);
    }
}
```

`[GenerateCommands]` enables complete command generation for the class. `[Command]` generates `ShowInfoCommand` and the code that creates its command instance, binds `ShowInfoAsync` to the compatible `CanShowInfo` callback, and integrates the command into the ViewModel command lifecycle.

Conceptually, the generator produces:

- `Name` and `Surname` properties with `PropertyChanged` notifications.
- `FullName` notifications when either generated property changes.
- An asynchronous `ShowInfoCommand` bound to `ShowInfoAsync`.
- Automatic discovery and binding of `CanShowInfo`.
- Command initialization, lookup, enumeration, and cleanup overrides.
- Cached event arguments for allocation-free notification paths.

## Command generation

### Complete command generation

Apply `[GenerateCommands]` to a partial class and `[Command]` to its command methods:

```csharp
using Minimal.Mvvm;
using Minimal.Mvvm.Wpf;

[GenerateCommands]
public partial class EditorViewModel : CommandViewModelBase
{
    private bool CanSave()
    {
        return HasChanges;
    }

    [Command(Setter = AccessModifier.Private)]
    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return SaveCoreAsync(cancellationToken);
    }
}
```

For every `[Command]` method, the generator:

- Generates the command property.
- Generates creation of the appropriate synchronous, `Task`-based, or `ValueTask`-based command instance.
- Resolves and binds a `CanExecute` callback when one is available.
- Generates overrides for command creation, lookup, enumeration, and cleanup.
- Preserves commands declared by base ViewModels through generated `base` calls.

The annotated class must be `partial` and derive from a compatible base class that supplies the command infrastructure, such as `CommandViewModelBase`, `ControlViewModelBase`, or `WindowViewModelBase` from `NuExt.Minimal.Mvvm.Wpf`.

### Command methods

A command method may be synchronous or asynchronous and may have one command parameter. Asynchronous methods may additionally accept a `CancellationToken` when supported by the corresponding command type.

The property name is derived from the method name. A trailing `Async` is removed before appending `Command`:

```csharp
[Command]
private void Refresh()
{
}

[Command]
private Task SaveAsync(CancellationToken cancellationToken)
{
    return SaveCoreAsync(cancellationToken);
}
```

This generates `RefreshCommand` and `SaveCommand`.

Use `PropertyName` when a different command-property name is required:

```csharp
[Command(PropertyName = "CommitCommand")]
private Task SaveAsync(CancellationToken cancellationToken)
{
    return SaveCoreAsync(cancellationToken);
}
```

### `CanExecute` resolution

When `CanExecute` is not specified, the generator looks for an accessible method named `Can{CommandMethodName}`. For an asynchronous command method, the trailing `Async` suffix is removed first.

```csharp
private bool CanSave()
{
    return HasChanges;
}

[Command]
private Task SaveAsync(CancellationToken cancellationToken)
{
    return SaveCoreAsync(cancellationToken);
}
```

Specify an explicit callback when the conventional name is not appropriate:

```csharp
private bool CanModify()
{
    return IsUsable;
}

[Command(CanExecute = nameof(CanModify))]
private void ExpandOrCollapse(bool expand)
{
}
```

Explicit callback lookup includes accessible methods declared by the current class and its base classes. The callback name is emitted as a method group, allowing the C# compiler to perform the final conversion check against the selected command constructor.

### Generated command lifecycle

Complete generation implements the command infrastructure required by the base class:

- `CreateCommands()` creates commands declared by the current class after calling the base implementation.
- `GetCurrentCommand(...)` maps a command method to its generated command property and delegates unknown methods to the base implementation.
- `GetAllCommands(...)` includes base commands and appends commands declared by the current class.
- `NullifyCommands()` clears generated command properties and then invokes the base implementation.

This infrastructure allows compatible ViewModel bases to cancel executing asynchronous commands, wait for command completion, raise `CanExecuteChanged`, and release command instances without runtime reflection.

### Command-property-only generation

Apply `[Notify]` to a method when the command property should be generated but command construction and lifecycle integration must remain explicit:

```csharp
using Minimal.Mvvm;

public partial class EditorViewModel : ViewModelBase
{
    public EditorViewModel()
    {
        SaveCommand = new AsyncCommand(SaveAsync, CanSave);
    }

    [Notify(Setter = AccessModifier.Private)]
    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return SaveCoreAsync(cancellationToken);
    }

    private bool CanSave()
    {
        return HasChanges;
    }
}
```

This mode generates the command property only. The application remains responsible for constructing the command and integrating it into any command lifecycle.

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
  Associates the task and its `CancellationTokenSource` with `propertyName`, cancelling and disposing the previous token source for that scope before replacing the tracked task.
- `CancelValidationTask(string propertyName = null)`  
  Cancels and disposes the tracked `CancellationTokenSource` for `propertyName`; with `null`, cancels all tracked validation operations.
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

When applied together with `[Command]` or `[Notify]`, enables automatic `CanExecute` reevaluation for the generated command property by subscribing to the WPF `CommandManager.RequerySuggested` event.

### Complete command generation

```csharp
using Minimal.Mvvm;
using Minimal.Mvvm.Wpf;

[GenerateCommands]
public partial class EditorViewModel : CommandViewModelBase
{
    private bool CanSave()
    {
        return HasChanges;
    }

    [Command(Setter = AccessModifier.Private), UseCommandManager]
    private void Save()
    {
    }
}
```

### Command-property-only generation

```csharp
using Minimal.Mvvm;

public partial class EditorViewModel : ViewModelBase
{
    public EditorViewModel()
    {
        SaveCommand = new RelayCommand(Save, CanSave);
    }

    [Notify, UseCommandManager]
    private IRelayCommand? _saveCommand;

    private void Save()
    {
    }

    private bool CanSave()
    {
        return true;
    }
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