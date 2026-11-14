using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Windows.Controls;
using NERO.AI;
using NERO.Core.Settings;

namespace NERO.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonNeroUserSettingsStore _settingsStore;
    private readonly GeminiDeviceConfiguration _geminiConfiguration;
    private readonly IGeminiApiClient _geminiClient;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    public MainWindow(JsonNeroUserSettingsStore settingsStore, GeminiDeviceConfiguration geminiConfiguration, IGeminiApiClient geminiClient)
    {
        _settingsStore = settingsStore;
        _geminiConfiguration = geminiConfiguration;
        _geminiClient = geminiClient;
        InitializeComponent();
        _trayIcon = CreateTrayIcon();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            LaunchAtStartupCheckBox.IsChecked = settings.LaunchAtStartup;
            StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
            MicrophoneCheckBox.IsChecked = false;
            SpeechLanguageComboBox.SelectedIndex = settings.SpeechLanguage switch
            {
                "en-IN" => 1,
                "hi-IN" => 2,
                _ => 0
            };
            try { NeroStartupRegistration.SetEnabled(settings.LaunchAtStartup); }
            catch (Exception ex) { SaveStatusText.Text = $"Startup registration unavailable: {ex.Message}"; }
            RefreshGeminiStatus();
            if (settings.StartMinimized) Hide();
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Could not load settings: {ex.Message}";
        }
    }

    private void RefreshGeminiStatus()
    {
        var configured = _geminiConfiguration.IsConfigured;
        GeminiConnectionStateText.Text = configured ? "API key saved securely on this device" : "Setup required — no device API key";
        GeminiStatusText.Text = configured ? "Gemini key is configured. Replace or remove it below." : "Enter your Gemini API key to connect this device.";
        RemoveGeminiKeyButton.IsEnabled = configured;
        AskGeminiButton.IsEnabled = configured;
    }

    private async void SaveGeminiKey_Click(object sender, RoutedEventArgs e)
    {
        var key = GeminiApiKeyPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            GeminiStatusText.Text = "Paste your Gemini API key first.";
            return;
        }

        SaveGeminiKeyButton.IsEnabled = false;
        RemoveGeminiKeyButton.IsEnabled = false;
        GeminiStatusText.Text = "Validating key with Google Gemini…";
        try
        {
            await _geminiConfiguration.ConfigureAsync(key);
            GeminiApiKeyPasswordBox.Clear();
            GeminiStatusText.Text = "Key validated and saved securely on this device.";
            RefreshGeminiStatus();
        }
        catch (OperationCanceledException)
        {
            GeminiStatusText.Text = "Gemini validation was cancelled.";
        }
        catch (Exception ex)
        {
            GeminiStatusText.Text = $"Could not configure Gemini: {ex.Message}";
        }
        finally
        {
            SaveGeminiKeyButton.IsEnabled = true;
            RemoveGeminiKeyButton.IsEnabled = _geminiConfiguration.IsConfigured;
        }
    }

    private async void AskGemini_Click(object sender, RoutedEventArgs e)
    {
        var prompt = GeminiPromptTextBox.Text;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            GeminiResponseText.Text = "Enter a question first.";
            return;
        }

        AskGeminiButton.IsEnabled = false;
        GeminiResponseText.Text = "NERO is waiting for Gemini…";
        try
        {
            var apiKey = _geminiConfiguration.GetApiKeyForRequest();
            GeminiResponseText.Text = await _geminiClient.GenerateTextAsync(apiKey, prompt);
        }
        catch (Exception ex)
        {
            GeminiResponseText.Text = $"Gemini request failed: {ex.Message}";
        }
        finally
        {
            AskGeminiButton.IsEnabled = _geminiConfiguration.IsConfigured;
        }
    }


    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open NERO", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Exit NERO", null, (_, _) => Dispatcher.Invoke(ExitFromTray));
        var icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "NERO",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        return icon;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _exitRequested = true;
        Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(1800, "NERO is running", "Open NERO from the system tray.", Forms.ToolTipIcon.Info);
    }

    public void DisposeTrayIcon()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void RemoveGeminiKey_Click(object sender, RoutedEventArgs e)
    {
        if (!_geminiConfiguration.IsConfigured)
        {
            GeminiStatusText.Text = "No Gemini key is saved on this device.";
            return;
        }

        var result = MessageBox.Show(
            "Remove the Gemini API key saved on this Windows device?",
            "Remove Gemini key",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _geminiConfiguration.Remove();
        GeminiApiKeyPasswordBox.Clear();
        RefreshGeminiStatus();
        GeminiStatusText.Text = "Gemini API key removed from this device.";
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var language = (SpeechLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
            var settings = new NeroUserSettings
            {
                LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked == true,
                StartMinimized = StartMinimizedCheckBox.IsChecked == true,
                MicrophoneListeningEnabled = false,
                SpeechLanguage = language
            };
            NeroStartupRegistration.SetEnabled(settings.LaunchAtStartup);
            await _settingsStore.SaveAsync(settings);
            SaveStatusText.Text = "Settings saved on this device.";
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Could not save settings: {ex.Message}";
        }
    }
}
