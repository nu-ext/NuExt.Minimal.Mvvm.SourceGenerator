using System.Windows;
using System.Windows.Controls;

namespace WpfAppSample
{

    /// <summary>
    /// Minimal helper to enable binding to PasswordBox.Password (string).
    /// Usage:
    ///   <PasswordBox
    ///       helpers:PasswordBoxHelper.BindPassword="True"
    ///       helpers:PasswordBoxHelper.Password="{Binding Password,
    ///           Mode=TwoWay, UpdateSourceTrigger=PropertyChanged,
    ///           ValidatesOnNotifyDataErrors=True}" />
    /// </summary>
    public static class PasswordBoxHelper
    {
        // main attached property that mirrors PasswordBox.Password
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.RegisterAttached(
                "Password",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPasswordPropertyChanged));

        public static string GetPassword(DependencyObject obj) =>
            (string)obj.GetValue(PasswordProperty);

        public static void SetPassword(DependencyObject obj, string value) =>
            obj.SetValue(PasswordProperty, value);

        // switch to hook/unhook PasswordChanged only when needed
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        public static bool GetBindPassword(DependencyObject obj) =>
            (bool)obj.GetValue(BindPasswordProperty);

        public static void SetBindPassword(DependencyObject obj, bool value) =>
            obj.SetValue(BindPasswordProperty, value);

        // guard to avoid recursive updates
        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        private static bool GetIsUpdating(DependencyObject obj) =>
            (bool)obj.GetValue(IsUpdatingProperty);

        private static void SetIsUpdating(DependencyObject obj, bool value) =>
            obj.SetValue(IsUpdatingProperty, value);

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box) return;

            if ((bool)e.OldValue)
                box.PasswordChanged -= HandlePasswordChanged;

            if ((bool)e.NewValue)
                box.PasswordChanged += HandlePasswordChanged;
        }

        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box) return;

            // temporarily detach to avoid duplicate events while we set Password
            box.PasswordChanged -= HandlePasswordChanged;

            if (!GetIsUpdating(box))
            {
                var newValue = e.NewValue as string ?? string.Empty;
                if (!string.Equals(box.Password, newValue))
                    box.Password = newValue;
            }

            box.PasswordChanged += HandlePasswordChanged;
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox box) return;

            SetIsUpdating(box, true);
            SetPassword(box, box.Password); // push current Password → binding
            SetIsUpdating(box, false);
        }

    }
}
