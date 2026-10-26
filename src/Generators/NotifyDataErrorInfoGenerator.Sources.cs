namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyDataErrorInfoGenerator
{
    private static string GetCodeSource(string nullable, string eventArgsCache, string dataErrorsChangedEventArgsCache) =>
        $$"""
        #region INotifyDataErrorInfo validation

        private readonly object _validationErrorsSyncRoot = new();
        private readonly object _validationTasksSyncRoot = new();
        private global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<string>>? _validationErrors;
        private global::System.Collections.Generic.Dictionary<string, (global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts)>? _validationTasks;
        private global::System.Threading.SynchronizationContext{{nullable}} _ui;
        private global::System.Threading.SendOrPostCallback{{nullable}} _errorsChanged;
        private global::System.Threading.SendOrPostCallback{{nullable}} _hasErrorsChanged;
        private volatile bool _hasErrors;

#if !NET8_0_OR_GREATER
        private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, string[]> s_emptyErrorsMap =
            new global::System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]>(
                new global::System.Collections.Generic.Dictionary<string, string[]>(0, global::System.StringComparer.Ordinal));
#endif

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private static void DisposeValidation(global::System.Threading.CancellationTokenSource cts)
        {
            try
            {
                cts.Dispose();
            }
            catch (global::System.ObjectDisposedException)
            {
                //do nothing
            }
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private static void CancelAndDisposeValidationOnCompletion((global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts) validation)
        {
            if (validation.task.IsCompleted)
            {
                DisposeValidation(validation.cts);
                return;
            }

            try
            {
                validation.cts.Cancel();
            }
            catch (global::System.ObjectDisposedException)
            {
                //do nothing
            }
            catch (global::System.AggregateException)
            {
                //do nothing
            }

            _ = validation.task.ContinueWith(static (_, state) =>
            {
                DisposeValidation((global::System.Threading.CancellationTokenSource)state!);
            }, validation.cts, global::System.Threading.CancellationToken.None, global::System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously, global::System.Threading.Tasks.TaskScheduler.Default);
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private void OnValidationTaskCompleted(global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts, string propertyName)
        {
            bool removed = false;
            lock (_validationTasksSyncRoot)
            {
                if (_validationTasks is { } validationTasks &&
                    validationTasks.TryGetValue(propertyName, out var validation) &&
                    global::System.Object.ReferenceEquals(validation.task, task) &&
                    global::System.Object.ReferenceEquals(validation.cts, cts))
                {
                    validationTasks.Remove(propertyName);
                    removed = true;
                }
            }

            if (removed)
            {
                DisposeValidation(cts);
            }
        }

        /// <summary>
        /// Associates an asynchronous validation task with the specified property, replacing any previous task.
        /// </summary>
        /// <param name="task">The validation task to track.</param>
        /// <param name="cts">
        /// The cancellation token source linked to <paramref name="task"/>. Ownership is transferred to this instance
        /// when the validation task is successfully registered. The caller must not dispose it after a successful
        /// registration. A dedicated cancellation token source must be provided for each validation task.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property associated with the validation operation. This value is normally supplied
        /// by the compiler when the method is called from a property setter.
        /// </param>
        /// <remarks>
        /// If <paramref name="propertyName"/> is <c>null</c> or empty, the validation task is not registered and ownership
        /// of <paramref name="cts"/> remains with the caller. If an existing task is present for
        /// <paramref name="propertyName"/>, cancellation is requested through its cancellation token source after the new
        /// task is stored. Each registered cancellation token source is disposed after its associated task completes.
        /// The validation operation is responsible for observing its cancellation token before publishing validation results.
        /// </remarks>
        public void SetValidationTask(global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            _ = task ?? throw new global::System.ArgumentNullException(nameof(task));
            _ = cts ?? throw new global::System.ArgumentNullException(nameof(cts));

            if (propertyName is not { Length: > 0 })
            {
                return;
            }

            (global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts)? oldValidation = null;
            lock (_validationTasksSyncRoot)
            {
                var validationTasks = _validationTasks ??= new(global::System.StringComparer.Ordinal);
                if (validationTasks.TryGetValue(propertyName, out var validation))
                {
                    if (global::System.Object.ReferenceEquals(validation.task, task) &&
                        global::System.Object.ReferenceEquals(validation.cts, cts))
                    {
                        return;
                    }

                    oldValidation = validation;
                }

                foreach (var registeredValidation in validationTasks.Values)
                {
                    if (global::System.Object.ReferenceEquals(registeredValidation.cts, cts))
                    {
                        throw new global::System.ArgumentException("The cancellation token source is already associated with another validation task.", nameof(cts));
                    }
                }

                validationTasks[propertyName] = (task, cts);
            }

            if (oldValidation is { } old)
            {
                CancelAndDisposeValidationOnCompletion(old);
            }

            _ = task.ContinueWith((completedTask, state) =>
            {
                var (tokenSource, name) = ((global::System.Threading.CancellationTokenSource cts, string propertyName))state!;
                OnValidationTaskCompleted(completedTask, tokenSource, name);
            }, (cts, propertyName), global::System.Threading.CancellationToken.None, global::System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously, global::System.Threading.Tasks.TaskScheduler.Default);
        }

        /// <summary>
        /// Requests cancellation of the validation task associated with the specified property.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property associated with the validation operation. This value is normally supplied
        /// by the compiler when the method is called from a property setter. When <c>null</c> or empty, the method
        /// performs no operation.
        /// </param>
        /// <remarks>
        /// This method requests cancellation through the associated
        /// <see cref="global::System.Threading.CancellationTokenSource"/>, removes the corresponding entry
        /// from the internal task registry, and disposes the token source after its associated task completes.
        /// </remarks>
        public void CancelValidationTask([global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            if (propertyName is not { Length: > 0 })
            {
                return;
            }

            (global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts) validation;
            lock (_validationTasksSyncRoot)
            {
                if (_validationTasks is null || !_validationTasks.TryGetValue(propertyName, out validation))
                {
                    return;
                }

                _validationTasks.Remove(propertyName);
            }

            CancelAndDisposeValidationOnCompletion(validation);
        }

        /// <summary>
        /// Requests cancellation of all tracked validation tasks.
        /// </summary>
        /// <remarks>
        /// Recommended to invoke during object teardown to request cancellation of all background validation operations.
        /// Their associated cancellation token sources are released after the tasks complete.
        /// </remarks>
        public void CancelAllValidationTasks()
        {
            (global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts)[] validations;
            lock (_validationTasksSyncRoot)
            {
                if (_validationTasks is not { Count: > 0 } validationTasks)
                {
                    return;
                }

                validations = new (global::System.Threading.Tasks.Task task, global::System.Threading.CancellationTokenSource cts)[validationTasks.Count];
                validationTasks.Values.CopyTo(validations, 0);
                validationTasks.Clear();
            }

            foreach (var validation in validations)
            {
                CancelAndDisposeValidationOnCompletion(validation);
            }
        }

        /// <summary>
        /// Adds a single validation error for the specified property; clears errors for that scope when <paramref name="message"/> is <c>null</c> or empty.
        /// </summary>
        /// <param name="message">The validation error message. When <c>null</c> or empty, errors for the scope are cleared.</param>
        /// <param name="propertyName">
        /// The name of the property; when <c>null</c> or empty, the operation targets entity-level errors.
        /// </param>
        /// <remarks>
        /// The operation is idempotent with respect to existing messages. Internal state changes are synchronized,
        /// and notifications are raised only when the target scope actually changes.
        /// </remarks>
        public void SetError(string{{nullable}} message, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            if (message is not { Length: > 0 })
            {
                ClearErrors(propertyName);
                return;
            }

            propertyName ??= string.Empty;// null or "" => entity-level

            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                var validationErrors = _validationErrors ??= new(global::System.StringComparer.Ordinal);
                if (validationErrors.TryGetValue(propertyName, out var errors))
                {
                    if (errors.Contains(message))
                    {
                        return;// no change
                    }

                    errors.Add(message);
                }
                else
                {
                    validationErrors[propertyName] = [message];
                }

                raiseHasErrorsChanged = UpdateHasErrors();
            }

            OnErrorsChanged(raiseHasErrorsChanged, propertyName);
        }

        /// <summary>
        /// Merges the specified validation messages into the target scope; clears errors when <paramref name="messages"/> is <c>null</c> or contains no items.
        /// </summary>
        /// <param name="messages">The validation messages to add. Duplicate and empty entries are ignored.</param>
        /// <param name="propertyName">
        /// The name of the property; when <c>null</c> or empty, messages apply to the entity-level scope.
        /// </param>
        /// <remarks>
        /// This method does not remove existing messages unless <paramref name="messages"/> is <c>null</c>
        /// or reduces to an empty set, in which case errors for the scope are cleared.
        /// </remarks>
        public void SetErrors(global::System.Collections.Generic.IEnumerable<string>{{nullable}} messages, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            if (messages is null)
            {
                ClearErrors(propertyName);
                return;
            }

            global::System.Collections.Generic.HashSet<string>{{nullable}} unique = null;
            global::System.Collections.Generic.List<string>{{nullable}} ordered = null;
            foreach (var msg in messages)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    unique ??= new(global::System.StringComparer.Ordinal);
                    if (unique.Add(msg))
                    {
                        (ordered ??= new()).Add(msg);
                    }
                }
            }

            if (unique is null || unique.Count == 0)
            {
                ClearErrors(propertyName);
                return;
            }

            propertyName ??= string.Empty;// null or "" => entity-level

            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                var validationErrors = _validationErrors ??= new(global::System.StringComparer.Ordinal);
                if (validationErrors.TryGetValue(propertyName, out var current))
                {
                    var currentSet = new global::System.Collections.Generic.HashSet<string>(current, global::System.StringComparer.Ordinal);
                    if (currentSet.IsSupersetOf(unique))
                    {
                        return;// no change
                    }

                    foreach (var msg in ordered!)
                    {
                        if (currentSet.Add(msg))
                        {
                            current.Add(msg);
                        }
                    }
                }
                else
                {
                    validationErrors[propertyName] = new(ordered!);
                }

                raiseHasErrorsChanged = UpdateHasErrors();
            }

            OnErrorsChanged(raiseHasErrorsChanged, propertyName);
        }

        /// <summary>
        /// Replaces the current validation messages for the specified scope with the given set.
        /// Passing <c>null</c> or an empty sequence clears errors.
        /// </summary>
        /// <param name="messages">The validation messages to set. Duplicate and empty entries are ignored.</param>
        /// <param name="propertyName">
        /// The name of the property; when <c>null</c> or empty, messages apply to the entity-level scope.
        /// </param>
        /// <remarks>
        /// Replaces the target scope atomically; raises <see cref="ErrorsChanged"/> only when the target scope actually changes.
        /// </remarks>
        public void ReplaceErrors(global::System.Collections.Generic.IEnumerable<string>{{nullable}} messages, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            if (messages is null)
            {
                ClearErrors(propertyName);
                return;
            }

            global::System.Collections.Generic.HashSet<string>{{nullable}} unique = null;
            global::System.Collections.Generic.List<string>{{nullable}} ordered = null;
            foreach (var msg in messages)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    unique ??= new(global::System.StringComparer.Ordinal);
                    if (unique.Add(msg))
                    {
                        (ordered ??= new()).Add(msg);
                    }
                }
            }

            if (unique is null || unique.Count == 0)
            {
                ClearErrors(propertyName);
                return;
            }

            propertyName ??= string.Empty;// null or "" => entity-level

            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                var validationErrors = _validationErrors ??= new(global::System.StringComparer.Ordinal);
                if (validationErrors.TryGetValue(propertyName, out var current) && current.Count == unique.Count)
                {
                    var currentSet = new global::System.Collections.Generic.HashSet<string>(current, global::System.StringComparer.Ordinal);
                    if (currentSet.SetEquals(unique))
                    {
                        return;// no change
                    }
                }

                validationErrors[propertyName] = new(ordered!);
                raiseHasErrorsChanged = UpdateHasErrors();
            }

            OnErrorsChanged(raiseHasErrorsChanged, propertyName);
        }

        /// <summary>
        /// Removes a validation message from the specified scope, if present.
        /// </summary>
        /// <param name="message">The validation error message.</param>
        /// <param name="propertyName">
        /// The name of the property; when <c>null</c> or empty, the operation targets entity-level errors.
        /// </param>
        /// <returns>
        /// <c>true</c> if the message was removed and the internal state changed; otherwise, <c>false</c>.
        /// </returns>
        public bool RemoveError(string message, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            propertyName ??= string.Empty;// null or "" => entity-level

            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                if (_validationErrors is null ||
                    !_validationErrors.TryGetValue(propertyName, out var errors) ||
                    !errors.Remove(message))
                {
                    return false;
                }

                if (errors.Count == 0)
                {
                    _validationErrors.Remove(propertyName);
                }

                raiseHasErrorsChanged = UpdateHasErrors();
            }

            OnErrorsChanged(raiseHasErrorsChanged, propertyName);
            return true;
        }

        /// <summary>
        /// Clears validation errors for the specified scope.
        /// </summary>
        /// <param name="propertyName">
        /// The property name; when <c>null</c> or empty, clears entity-level errors.
        /// </param>
        public void ClearErrors([global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            propertyName ??= string.Empty;// null or "" => entity-level

            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                if (_validationErrors is null || !_validationErrors.Remove(propertyName))
                {
                    return;
                }

                raiseHasErrorsChanged = UpdateHasErrors();
            }

            OnErrorsChanged(raiseHasErrorsChanged, propertyName);
        }

        /// <summary>
        /// Clears all validation errors for the current instance.
        /// </summary>
        public void ClearAllErrors()
        {
            string[] properties;
            bool raiseHasErrorsChanged;
            lock (_validationErrorsSyncRoot)
            {
                if (_validationErrors is not { Count: > 0 } validationErrors)
                {
                    return;
                }

                properties = new string[validationErrors.Count];
                validationErrors.Keys.CopyTo(properties, 0);
                validationErrors.Clear();
                raiseHasErrorsChanged = UpdateHasErrors();
            }

            foreach (var propertyName in properties)
            {
                OnErrorsChanged(false, propertyName);
            }

            if (raiseHasErrorsChanged)
            {
                OnHasErrorsChanged();
            }
        }

        /// <summary>
        /// Gets a snapshot of validation error messages for the specified property or for the entity.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to retrieve errors for; when <c>null</c> or empty, returns entity-level errors.
        /// </param>
        /// <returns>
        /// A snapshot (copy) of the current validation messages for the requested scope. Returns an empty sequence if none are present.
        /// </returns>
        public global::System.Collections.Generic.IEnumerable<string> GetErrors(string{{nullable}} propertyName = null)
        {
            propertyName ??= string.Empty;// null or "" => entity-level

            lock (_validationErrorsSyncRoot)
            {
                if (_validationErrors is null ||
                    !_validationErrors.TryGetValue(propertyName, out var errors) ||
                    errors.Count == 0)
                {
                    return [];
                }

                return errors.ToArray();
            }
        }

        /// <summary>
        /// Returns a snapshot of all current validation messages grouped by property name,
        /// including the entity-level key (<see cref="string.Empty"/>).
        /// </summary>
        public global::System.Collections.Generic.IReadOnlyDictionary<string, string[]> GetErrorsSnapshot()
        {
            global::System.Collections.Generic.Dictionary<string, string[]> copy;
            lock (_validationErrorsSyncRoot)
            {
                if (_validationErrors is not { Count: > 0 } validationErrors)
                {
        #if NET8_0_OR_GREATER
                    return global::System.Collections.Frozen.FrozenDictionary<string, string[]>.Empty;
        #else
                    return s_emptyErrorsMap;
        #endif
                }

                copy = new(validationErrors.Count, global::System.StringComparer.Ordinal);
                foreach (var pair in validationErrors)
                {
                    if (pair.Value.Count > 0)
                    {
                        copy[pair.Key] = pair.Value.ToArray();
                    }
                }
            }

        #if NET8_0_OR_GREATER
            return copy.Count == 0 ? global::System.Collections.Frozen.FrozenDictionary<string, string[]>.Empty :
                global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(copy, global::System.StringComparer.Ordinal);
        #else
            return copy.Count == 0 ? s_emptyErrorsMap :
                new global::System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]>(copy);
        #endif
        }

        /// <summary>
        /// Returns whether the specified scope currently has validation errors.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to check; when <c>null</c> or empty, checks the entity-level scope.
        /// </param>
        /// <returns><c>true</c> if the scope currently has errors; otherwise, <c>false</c>.</returns>
        public bool HasErrorsFor([global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            propertyName ??= string.Empty;// null or "" => entity-level

            lock (_validationErrorsSyncRoot)
            {
                return _validationErrors is { } validationErrors &&
                       validationErrors.TryGetValue(propertyName, out var errors) &&
                       errors.Count > 0;
            }
        }

        /// <summary>
        /// Caches a non-null current <see cref="System.Threading.SynchronizationContext"/> for UI dispatching.
        /// </summary>
        /// <param name="current">Receives the current context snapshot.</param>
        private void EnsureSynchronizationContext(ref global::System.Threading.SynchronizationContext{{nullable}} current)
        {
            current = global::System.Threading.SynchronizationContext.Current;
            if (_ui is null && current is not null)
            {
                _ui = current;
            }
        }

        /// <summary>
        /// Raises <see cref="ErrorsChanged"/> for the specified scope. If invoked from a non-UI thread
        /// and a UI context was captured, the notification is dispatched to that context.
        /// </summary>
        /// <param name="raiseHasErrorsChanged">
        /// Indicates whether <see cref="global::System.ComponentModel.INotifyDataErrorInfo.HasErrors"/> has changed
        /// and a corresponding <see cref="global::System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> notification should be raised.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property; <c>null</c> or empty indicates the entity-level scope.
        /// </param>
        private void OnErrorsChanged(bool raiseHasErrorsChanged, [global::System.Runtime.CompilerServices.CallerMemberName] string{{nullable}} propertyName = null)
        {
            var handler = errorsChanged;
            if (handler is null)
            {
                if (raiseHasErrorsChanged)
                {
                    OnHasErrorsChanged();
                }
                return;
            }

            global::System.Threading.SynchronizationContext{{nullable}} current = null;
            EnsureSynchronizationContext(ref current);

            var args = {{dataErrorsChangedEventArgsCache}}.Get(propertyName);
            if (_ui is not null && current != _ui)
            {
                _ui.Post(_errorsChanged ??= OnErrorsChanged, (args, raiseHasErrorsChanged));
                return;
            }

            handler(this, args);
            if (raiseHasErrorsChanged)
            {
                OnPropertyChanged({{eventArgsCache}}.HasErrorsPropertyChanged);
            }
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private void OnHasErrorsChanged()
        {
            global::System.Threading.SynchronizationContext{{nullable}} current = null;
            EnsureSynchronizationContext(ref current);

            if (_ui is not null && current != _ui)
            {
                _ui.Post(_hasErrorsChanged ??= OnHasErrorsChanged, null);
                return;
            }

            OnPropertyChanged({{eventArgsCache}}.HasErrorsPropertyChanged);
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private void OnErrorsChanged(object{{nullable}} state)
        {
            var (args, raiseHasErrorsChanged) = ((global::System.ComponentModel.DataErrorsChangedEventArgs, bool))state!;
            errorsChanged?.Invoke(this, args);
            if (raiseHasErrorsChanged)
            {
                OnPropertyChanged({{eventArgsCache}}.HasErrorsPropertyChanged);
            }
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private void OnHasErrorsChanged(object{{nullable}} state)
        {
            OnPropertyChanged({{eventArgsCache}}.HasErrorsPropertyChanged);
        }

        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        private bool UpdateHasErrors()
        {
            bool hasErrors = _validationErrors is { Count: > 0 };
            if (_hasErrors == hasErrors)
            {
                return false;
            }

            _hasErrors = hasErrors;
            return true;
        }

        #endregion

        #region INotifyDataErrorInfo implementation

        private event global::System.EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>{{nullable}} errorsChanged;

        /// <inheritdoc />
        public event global::System.EventHandler<global::System.ComponentModel.DataErrorsChangedEventArgs>{{nullable}} ErrorsChanged
        {
            add
            {
                global::System.Threading.SynchronizationContext{{nullable}} current = null;
                EnsureSynchronizationContext(ref current);
                errorsChanged += value;
            }
            remove
            {
                errorsChanged -= value;
            }
        }

        /// <inheritdoc />
        global::System.Collections.IEnumerable global::System.ComponentModel.INotifyDataErrorInfo.GetErrors(string{{nullable}} propertyName)
            => GetErrors(propertyName);

        /// <inheritdoc />
        public bool HasErrors => _hasErrors;

        #endregion
""";
}
