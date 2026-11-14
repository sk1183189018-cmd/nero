using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Windows.Controls;
using NERO.AI;
using NERO.Core.Settings;
using NERO.Voice;

namespace NERO.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonNeroUserSettingsStore _settingsStore;
    private readonly GeminiDeviceConfiguration _geminiConfiguration;
    private readonly IGeminiApiClient _geminiClient;
    private readonly IMicrophoneRecorder _microphoneRecorder;
    private readonly ITextToSpeech _textToSpeech;
    private CancellationTokenSource? _voiceRecordingCancellation;
    private Task<byte[]>? _voiceRecordingTask;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    public MainWindow(JsonNeroUserSettingsStore settingsStore, GeminiDeviceConfiguration geminiConfiguration, IGeminiApiClient geminiClient, IMicrophoneRecorder microphoneRecorder, ITextToSpeech textToSpeech)
    {
        _settingsStore = settingsStore;
        _geminiConfiguration = geminiConfiguration;
        _geminiClient = geminiClient;
        _microphoneRecorder = microphoneRecorder;
        _textToSpeech = textToSpeech;
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
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        CancelVoiceRecording();
        _textToSpeech.Stop();
        Hide();
        _trayIcon.ShowBalloonTip(1800, "NERO is running", "Open NERO from the system tray.", Forms.ToolTipIcon.Info);
    }

    public void DisposeTrayIcon()
    {
        CancelVoiceRecording();
        _textToSpeech.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void StartVoiceRecording_Click(object sender, RoutedEventArgs e)
    {
        if (!_geminiConfiguration.IsConfigured)
        {
            VoiceStatusText.Text = "Set up your Gemini API key before using voice input.";
            return;
        }

        try
        {
            _voiceRecordingCancellation = new CancellationTokenSource();
            _voiceRecordingTask = _microphoneRecorder.RecordWaveUntilCancelledAsync(_voiceRecordingCancellation.Token);
            StartVoiceRecordingButton.IsEnabled = false;
            StopVoiceRecordingButton.IsEnabled = true;
            VoiceTranscriptText.Text = string.Empty;
            VoiceStatusText.Text = "Microphone recording is active on this PC. Press Stop & send when finished.";
        }
        catch (Exception ex)
        {
            CancelVoiceRecording();
            VoiceStatusText.Text = $"Could not start microphone: {ex.Message}";
        }
    }

    private async void StopVoiceRecording_Click(object sender, RoutedEventArgs e)
    {
        var cancellation = _voiceRecordingCancellation;
        var recording = _voiceRecordingTask;
        if (cancellation is null || recording is null) return;

        StopVoiceRecordingButton.IsEnabled = false;
        VoiceStatusText.Text = "Stopping microphone and preparing the recording…";
        cancellation.Cancel();
        try
        {
            var audio = await recording;
            if (audio.Length <= 44)
            {
                VoiceStatusText.Text = "No usable audio was recorded.";
                return;
            }

            VoiceStatusText.Text = "Sending this recording to Gemini for transcription…";
            var apiKey = _geminiConfiguration.GetApiKeyForRequest();
            var transcript = await _geminiClient.TranscribeAudioAsync(apiKey, audio);
            VoiceTranscriptText.Text = transcript;
            VoiceStatusText.Text = "Transcript received. Asking Gemini for a response…";
            var answer = await _geminiClient.GenerateTextAsync(
                apiKey,
                "Respond helpfully and naturally to this spoken user request. The transcript may contain Hindi, Hinglish, or English. Do not claim to have performed computer actions; just answer the request.\n\nUser said: " + transcript);
            GeminiResponseText.Text = answer;
            VoiceStatusText.Text = "Voice request completed. Speaking response…";
            try
            {
                await _textToSpeech.SpeakAsync(answer);
                VoiceStatusText.Text = "Voice request completed.";\n            }
            catch (Exception speechError)
            {
                SpeechStatusText.Text = $"Text reply is ready, but speech output is unavailable: {speechError.Message}";
                VoiceStatusText.Text = "Voice request completed with text response.";\n            }
        }
        catch (OperationCanceledException)
        {
            VoiceStatusText.Text = "Voice recording was cancelled. No audio was sent.";
        }
        catch (Exception ex)
        {
            VoiceStatusText.Text = $"Voice request failed: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_voiceRecordingCancellation, cancellation))
            {
                _voiceRecordingCancellation = null;
                _voiceRecordingTask = null;
            }
            cancellation.Dispose();
            StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured;
            StopVoiceRecordingButton.IsEnabled = false;
        }
    }

    private void CancelVoiceRecording()
    {
        var cancellation = _voiceRecordingCancellation;
        var recording = _voiceRecordingTask;
        _voiceRecordingCancellation = null;
        _voiceRecordingTask = null;
        if (cancellation is null) return;

        StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured;
        StopVoiceRecordingButton.IsEnabled = false;
        VoiceStatusText.Text = "Voice recording cancelled because NERO was hidden or closed. No audio was sent.";
        cancellation.Cancel();
        if (recording is null)
        {
            cancellation.Dispose();
            return;
        }

        _ = recording.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async void SpeakResponse_Click(object sender, RoutedEventArgs e)
    {
        var response = GeminiResponseText.Text;
        if (string.IsNullOrWhiteSpace(response))
        {
            SpeechStatusText.Text = "There is no response to speak yet.";
            return;
        }

        SpeakResponseButton.IsEnabled = false;
        SpeechStatusText.Text = "Speaking with an installed Windows voice…";
        try
        {
            await _textToSpeech.SpeakAsync(response);
            SpeechStatusText.Text = "Speech finished.";
        }
        catch (OperationCanceledException)
        {
            SpeechStatusText.Text = "Speech stopped.";
        }
        catch (Exception ex)
        {
            SpeechStatusText.Text = $"Speech output unavailable: {ex.Message}";
        }
        finally
        {
            SpeakResponseButton.IsEnabled = true;
        }
    }

    private void StopSpeaking_Click(object sender, RoutedEventArgs e)
    {
        _textToSpeech.Stop();
        SpeechStatusText.Text = "Stopping speech…";
    }

    private void RemoveGeminiKey_Click(object sender, RoutedEventArgs e)
    {
        if (!_geminiConfiguration.IsConfigured)
        {
            GeminiStatusText.Text = "No Gemini key is saved on this device.";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "Remove the Gemini API key saved on this Windows device?",
            "Remove Gemini key",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

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
