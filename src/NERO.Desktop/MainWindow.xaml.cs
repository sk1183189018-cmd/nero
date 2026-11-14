using System.Windows;
using System.Windows.Controls;
using NERO.Core.Settings;

namespace NERO.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonNeroUserSettingsStore _settingsStore;

    public MainWindow(JsonNeroUserSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
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
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Could not load settings: {ex.Message}";
        }
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
