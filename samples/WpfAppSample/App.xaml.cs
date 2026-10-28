using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using WpfAppSample.ViewModels;
using WpfAppSample.Views;

namespace WpfAppSample;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        string culture = CultureInfo.CurrentUICulture.IetfLanguageTag;
        var langFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", $"local.{culture}.json");
        if (File.Exists(langFilePath))
        {
            Loc.LoadFromJsonFile(langFilePath);
        }

        // Prevent auto-shutdown when the dialog closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var loginVm = new LoginViewModel();
        try
        {
            var loginWindow = new Window
            {
                Content = new LoginView(),
                DataContext = loginVm,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Title = Loc.Login.Title
            };

            await loginVm.InitializeAsync();

            bool ok = loginWindow.ShowDialog() == true;

            await loginVm.UninitializeAsync();

            if (!ok)
            {
                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            (MainWindow = new MainWindow() { DataContext = new MainWindowViewModel() }).Show();
        }
        catch (Exception ex)
        {
            Debug.Assert(ex is OperationCanceledException, "Error while initialization: " + ex.Message);
            await loginVm.UninitializeAsync();
            Shutdown();
        }
    }
}