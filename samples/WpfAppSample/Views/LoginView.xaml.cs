using System;
using System.Windows;
using System.Windows.Controls;
using WpfAppSample.Infrastructure;

namespace WpfAppSample.Views;

/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ICloseRequest oldCloseRequest)
        {
            oldCloseRequest.CloseRequested -= OnCloseRequested;
        }
        if (e.NewValue is ICloseRequest closeRequest)
        {
            closeRequest.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, CloseRequestEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }
        try
        {
            if (e.DialogResult.HasValue)
            {
                window.DialogResult = e.DialogResult; // valid only for modal windows
            }
        }
        catch (InvalidOperationException)
        {
            // Non-modal path: ignore and just close
        }

        window.Close();
    }
}
