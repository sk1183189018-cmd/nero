using System.Windows;
using System.Windows.Controls;
using NERO.AI;
using NERO.Core.Settings;

namespace NERO.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonNeroUserSettingsStore _settingsStore;
    private readonly GeminiDeviceConfiguration _geminiConfiguration;

    public MainWindow(JsonNeroUserSettingsStore settingsStore, GeminiDeviceConfiguration geminiConfiguration)
    {
        _settingsStore = settingsStore;
        _geminiConfiguration = geminiConfiguration;
        InitializeComponent();
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
            RefreshGeminiStatus();
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
            await _settingsStore.SaveAsync(settings);
            SaveStatusText.Text = "Settings saved on this device.";
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Could not save settings: {ex.Message}";
        }
    }
}
