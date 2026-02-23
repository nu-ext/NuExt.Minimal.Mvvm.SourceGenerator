using Minimal.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WpfAppSample.Infrastructure;
using WpfAppSample.Services;

namespace WpfAppSample.ViewModels
{
    [NotifyDataErrorInfo]
    internal sealed partial class LoginViewModel : ViewModelBase, INotifyDataErrorInfo, ICloseRequest
    {
        private readonly IAuthService _auth;
        private readonly CancellationTokenSource _cts = new();

        public LoginViewModel(IAuthService auth)
        {
            _auth = auth;
        }

        public LoginViewModel() : this(new FakeAuthService()) { }

        // ---------------------------- Properties ----------------------------

        [Notify] private string _userName = string.Empty;

        [Notify] private string _password = string.Empty;

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
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, 
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
                errors.Add(Loc.User_name_is_required);
            else if (UserName.Length > MaxUserNameLength)
                errors.Add(string.Format(Loc.User_name_is_too_long__max_Arg0_, MaxUserNameLength));
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
                locked = await _auth.IsUserLockedAsync(userName, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            if (locked)
            {
                SetError(Loc.This_user_is_locked, nameof(UserName));
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
                errors.Add(Loc.Password_is_required);
            else if (Password.Length < MinPasswordLength)
                errors.Add(string.Format(Loc.Password_must_be_at_least_Arg0_characters_, MinPasswordLength));
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
            var ok = await _auth.LoginAsync(UserName, Password, RememberMe, ct);
            if (!ok)
            {
                // entity-level error, not tied to a specific field
                SetError(Loc.Login_failed__Check_your_credentials, string.Empty);
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
}
