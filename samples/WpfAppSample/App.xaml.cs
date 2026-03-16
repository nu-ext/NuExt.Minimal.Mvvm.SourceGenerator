using Minimal.Mvvm;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
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
        LoadLocalization(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", $"local.{culture}.json"), typeof(Loc));

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
                Title = Loc.Login
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

    public static void LoadLocalization(string langFilePath, Type type)
    {
        Debug.Assert(File.Exists(langFilePath), $"File doesn't exist: {langFilePath}");
        if (!File.Exists(langFilePath)) return;
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langFilePath));
        if (translations is not { Count: > 0 })
        {
            return;
        }
        translations = translations.ToDictionary(pair => LocalizeAttribute.StringToValidPropertyName(pair.Key), pair => pair.Value);
        var props = type.GetProperties();
        foreach (var prop in props)
        {
            //Debug.Assert(!prop.CanWrite || translations.ContainsKey(prop.Name), $"Can't find translation for {prop.Name}");
            if (prop.CanWrite && translations.TryGetValue(prop.Name, out var text))
            {
                prop.SetValue(null, text);
            }
        }
    }

}
