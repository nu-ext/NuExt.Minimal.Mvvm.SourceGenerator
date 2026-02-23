namespace Minimal.Mvvm.SourceGenerator
{
    partial class Generator
    {
        #region Sources

        internal static readonly (string hintName, string source)[] Sources = [
            (hintName : "Minimal.Mvvm.AccessModifier.g.cs", source : """
            /// <summary>
            /// Enum to define access modifiers.
            /// </summary>
            internal enum AccessModifier
            {
                Default = 0,
                Public = 6,
                ProtectedInternal = 5,
                Internal = 4,
                Protected = 3,
                PrivateProtected = 2,
                Private = 1,
            }
            """),

            (hintName : "Minimal.Mvvm.CustomAttributeAttribute.g.cs", source : """
            using System;

            namespace Minimal.Mvvm
            {
                /// <summary>
                /// A custom attribute that allows specifying a fully qualified attribute name to be applied to a generated property.
                /// </summary>
                [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
                internal sealed class CustomAttributeAttribute : Attribute
                {
                    /// <summary>
                    /// Initializes a new instance of the <see cref="CustomAttributeAttribute"/> class with the specified fully qualified attribute name.
                    /// </summary>
                    /// <param name="fullyQualifiedAttributeName">The fully qualified name of the attribute to apply.</param>
                    public CustomAttributeAttribute(string fullyQualifiedAttributeName)
                    {
                        FullyQualifiedAttributeName = fullyQualifiedAttributeName;
                    }
            
                    /// <summary>
                    /// Gets the fully qualified name of the attribute to apply.
                    /// </summary>
                    public string FullyQualifiedAttributeName { get; }
                }
            }
            """),

            (hintName : "Minimal.Mvvm.LocalizeAttribute.g.cs", source : """
            using System;

            namespace Minimal.Mvvm
            {
                /// <summary>
                /// Specifies that the target class should be localized using the provided JSON file. JSON file should be specified in AdditionalFiles.
                /// </summary>
                [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                internal sealed class LocalizeAttribute : Attribute
                {
                    /// <summary>
                    /// Initializes a new instance of the <see cref="LocalizeAttribute"/> class with the specified JSON file name.
                    /// </summary>
                    /// <param name="jsonFileName">The JSON file name.</param>
                    public LocalizeAttribute(string jsonFileName)
                    {

                    }

                    public static string StringToValidPropertyName(string key)
                    {
                        _ = key ?? throw new ArgumentNullException(nameof(key));
            
                        int start = 0;
                        int end = key.Length - 1;
            
                        while (start <= end && char.IsWhiteSpace(key[start])) start++;
                        while (end >= start && char.IsWhiteSpace(key[end])) end--;
            
                        int len = end - start + 1;
                        if (len <= 0) return "_";
            
            #if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        return string.Create(len, (key, start), static (dest, state) =>
                        {
                            var src = state.key.AsSpan(state.start, dest.Length);
            
                            char c0 = src[0];
                            dest[0] = char.IsLetter(c0) ? char.ToUpperInvariant(c0) : '_';
            
                            for (int i = 1; i < dest.Length; i++)
                            {
                                char ch = src[i];
                                dest[i] = char.IsLetterOrDigit(ch) ? ch : '_';
                            }
                        });
            #else
                        var buf = new char[len];
            
                        char c0 = key[start];
                        buf[0] = char.IsLetter(c0) ? char.ToUpperInvariant(c0) : '_';
            
                        int index = start + 1;
                        for (int i = 1; i < len; i++)
                        {
                            char ch = key[index++];
                            buf[i] = char.IsLetterOrDigit(ch) ? ch : '_';
                        }
                        return new string(buf);
            #endif
                    }
                }
            }
            """),

            (hintName : "Minimal.Mvvm.NotifyAttribute.g.cs", source : """
            using System;

            namespace Minimal.Mvvm
            {
                /// <summary>
                /// Attribute to mark a field or property or method for code generation of property and associated callback methods.
                /// </summary>
                [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
                internal sealed class NotifyAttribute : Attribute
                {
                    /// <summary>
                    /// Initializes a new instance of the <see cref="NotifyAttribute"/> class.
                    /// </summary>
                    public NotifyAttribute()
                    {
                    }

                    /// <summary>
                    /// Initializes a new instance of the <see cref="NotifyAttribute"/> class with the specified property name.
                    /// </summary>
                    /// <param name="propertyName">The name of the property.</param>
                    public NotifyAttribute(string propertyName)
                    {
                        PropertyName = propertyName;
                    }

                    /// <summary>
                    /// Gets or sets the name of the property.
                    /// </summary>
                    public string PropertyName { get; set; }

                    /// <summary>
                    /// Gets or sets the name of the callback method.
                    /// </summary>
                    public string CallbackName { get; set; }

                    /// <summary>
                    /// Gets or sets a value indicating whether to prefer method with parameter for callback.
                    /// </summary>
                    public bool PreferCallbackWithParameter { get; set; }

                    /// <summary>
                    /// Gets or sets the access modifier for the getter.
                    /// </summary>
                    public AccessModifier Getter { get; set; }

                    /// <summary>
                    /// Gets or sets the access modifier for the setter.
                    /// </summary>
                    public AccessModifier Setter { get; set; }
                }
            }
            """),
             (hintName : "Minimal.Mvvm.AlsoNotifyAttribute.g.cs", source : """
            using System;

            namespace Minimal.Mvvm
            {
                /// <summary>
                /// Attribute to specify additional properties to notify when the annotated property changes.
                /// </summary>
                [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
                internal sealed class AlsoNotifyAttribute : Attribute
                {
                    /// <summary>
                    /// Initializes a new instance of the <see cref="AlsoNotifyAttribute"/> class with the specified property names.
                    /// </summary>
                    /// <param name="propertyNames">The names of the properties to notify.</param>
                    public AlsoNotifyAttribute(params string[] propertyNames)
                    {
                        PropertyNames = propertyNames;
                    }

                    /// <summary>
                    /// Gets the names of the properties to notify.
                    /// </summary>
                    public string[] PropertyNames { get; }
                }
            }
            """),
             (hintName : "Minimal.Mvvm.NotifyDataErrorInfoAttribute.g.cs", source : """
            using System;

            namespace Minimal.Mvvm
            {
                /// <summary>
                /// Attribute to mark a class for code generation if it inherited from <see cref="System.ComponentModel.INotifyDataErrorInfo"/> .
                /// </summary>
                [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                internal sealed class NotifyDataErrorInfoAttribute : Attribute
                {
                    /// <summary>
                    /// Initializes a new instance of the <see cref="NotifyDataErrorInfoAttribute"/> class.
                    /// </summary>
                    public NotifyDataErrorInfoAttribute()
                    {

                    }
                }
            }
            """),
            (hintName : "Minimal.Mvvm.UseCommandManagerAttribute.g.cs", source : """
                using System;

                namespace Minimal.Mvvm
                {
                    /// <summary>
                    /// Enables automatic IRelayCommand.CanExecute re-evaluation for generated commands
                    /// by subscribing to the WPF CommandManager.RequerySuggested event.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
                    internal sealed class UseCommandManagerAttribute : Attribute
                    {
                        /// <summary>
                        /// Initializes a new instance of the <see cref="UseCommandManagerAttribute"/> class.
                        /// </summary>
                        public UseCommandManagerAttribute()
                        {

                        }
                    }
                }
                """),
        ];

        internal static readonly (string hintName, string source) RequerySuggestedEventManagerSource = ("Minimal.Mvvm.RequerySuggestedEventManager.g.cs", """
            namespace Minimal.Mvvm
            {
                internal static class RequerySuggestedEventManager
                {
                    private static readonly global::System.Reflection.MethodInfo s_handlerMethod = typeof(RequerySuggestedEventManager).GetMethod(nameof(HandleRequerySuggested)) ?? throw new global::System.NullReferenceException();

                    public static void HandleRequerySuggested(global::Minimal.Mvvm.IRelayCommand? command, object? sender, global::System.EventArgs e)
                    {
                        command?.RaiseCanExecuteChanged();
                    }

                    public static void AddHandler(global::Minimal.Mvvm.IRelayCommand? command)
                    {
                        if (command == null) return;
                        var handler = (global::System.EventHandler)global::System.Delegate.CreateDelegate(typeof(global::System.EventHandler), command, s_handlerMethod);
                        global::System.Windows.Input.CommandManager.RequerySuggested -= handler;
                        global::System.Windows.Input.CommandManager.RequerySuggested += handler;
                    }

                    public static void RemoveHandler(global::Minimal.Mvvm.IRelayCommand? command)
                    {
                        if (command == null) return;
                        var handler = (global::System.EventHandler)global::System.Delegate.CreateDelegate(typeof(global::System.EventHandler), command, s_handlerMethod);
                        global::System.Windows.Input.CommandManager.RequerySuggested -= handler;
                    }
                }
            }
            """);

        #endregion
    }
}
