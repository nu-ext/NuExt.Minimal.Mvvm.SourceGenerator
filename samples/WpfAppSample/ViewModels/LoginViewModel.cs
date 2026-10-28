using Minimal.Mvvm;
using System.ComponentModel;
using WpfAppSample.Infrastructure;
using WpfAppSample.Services;

namespace WpfAppSample.ViewModels;

[NotifyDataErrorInfo]
internal sealed partial class LoginViewModel(IAuthService auth) : ViewModelBase, INotifyDataErrorInfo, ICloseRequest
{
    private readonly CancellationTokenSource _cts = new();

    public LoginViewModel() : this(new FakeAuthService()) { }

    // ---------------------------- Properties ----------------------------

    [Notify] public partial string UserName { get; set; }

    [Notify] public partial string Password { get; set; }

    [Notify] public partial bool RememberMe { get; set; }

    // ------------------------------ Events ------------------------------

    public event EventHandler<CloseRequestEventArgs>? CloseRequested;

    // -------------------------- Event Handlers --------------------------

    private void LoginViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UserName):
                CancelValidationTask(nameof(UserName));
                ValidateUserNameSync();// sync way
                // async way
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                var task = ValidateUserNameAsync(UserName, cts.Token);
                task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, 
                    TaskContinuationOptions.OnlyOnFaulted | 
                    TaskContinuationOptions.ExecuteSynchronously, 
                    TaskScheduler.Default);
                SetValidationTask(task, cts, nameof(UserName));
                break;

            case nameof(Password):
                ValidatePasswordSync();// sync way
                break;

            case nameof(RememberMe):
                break;
        }
    }

    // ---------------------------- Validation ----------------------------

    private const int MaxUserNameLength = 20;
    private const int MinPasswordLength = 4;

    private void ValidateUserNameSync()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(UserName))
            errors.Add(Loc.Validation.UserNameRequired);
        else if (UserName.Length > MaxUserNameLength)
            errors.Add(string.Format(Loc.Validation.UserNameTooLong, MaxUserNameLength));
        ReplaceErrors(errors, nameof(UserName));
    }

    private async Task ValidateUserNameAsync(string userName, CancellationToken ct)
    {
        // debounce
        try { await Task.Delay(250, ct); }
        catch (OperationCanceledException) { return; }

        // if sync errors still exist — skip async check
        if (HasErrorsFor(nameof(UserName)))
        {
            return;
        }

        // ensure value is up-to-date
        if (!string.Equals(UserName, userName, StringComparison.Ordinal))
        {
            return;
        }

        bool locked;
        try
        {
            locked = await auth.IsUserLockedAsync(userName, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }

        if (locked)
        {
            SetError(Loc.Validation.UserLocked, nameof(UserName));
        }
        else
        {
            ClearErrors(nameof(UserName));
        }
    }

    private void ValidatePasswordSync()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Password))
            errors.Add(Loc.Validation.PasswordRequired);
        else if (Password.Length < MinPasswordLength)
            errors.Add(string.Format(Loc.Validation.PasswordTooShort, MinPasswordLength));
        ReplaceErrors(errors, nameof(Password));
    }

    // ------------------------- Command Methods --------------------------

    [Notify]
    private void Cancel()
    {
        Close();
    }

    private bool CanLogin()
        => !HasErrorsFor(nameof(UserName)) && !HasErrorsFor(nameof(Password))
        && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    [Notify, UseCommandManager]
    private async Task LoginAsync(CancellationToken ct)
    {
        ClearErrors(string.Empty);//entity-level
        var ok = await auth.LoginAsync(UserName, Password, RememberMe, ct);
        if (!ok)
        {
            // entity-level error, not tied to a specific field
            SetError(Loc.Validation.LoginFailed, string.Empty);
            return;
        }

        Close(true);
    }

    // ----------------------------- Methods ------------------------------

    private void Close(bool? dialogResult = null)
    {
        CloseRequested?.Invoke(this, new CloseRequestEventArgs(dialogResult));
    }

    protected override async Task InitializeAsyncCore(CancellationToken cancellationToken)
    {
        await base.InitializeAsyncCore(cancellationToken);

        CancelCommand = new RelayCommand(Cancel);
        LoginCommand = new AsyncCommand(LoginAsync, CanLogin);

        PropertyChanged += LoginViewModel_PropertyChanged;
    }

    protected override async Task UninitializeAsyncCore(CancellationToken cancellationToken)
    {
        PropertyChanged -= LoginViewModel_PropertyChanged;
        CancelAllValidationTasks();
#if NET8_0_OR_GREATER
        await _cts.CancelAsync();
#else
        _cts.Cancel();
#endif
        _cts.Dispose();
        CancelCommand = null;
        LoginCommand = null;
        await base.UninitializeAsyncCore(cancellationToken);
    }
}
