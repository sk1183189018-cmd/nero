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
    private readonly IWakeWordListener _wakeWordListener;
    private CancellationTokenSource? _wakeWordCancellation;
    private Task? _wakeWordTask;
    private bool _settingsLoaded;
    private CancellationTokenSource? _voiceRecordingCancellation;
    private Task<byte[]>? _voiceRecordingTask;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    public MainWindow(JsonNeroUserSettingsStore settingsStore, GeminiDeviceConfiguration geminiConfiguration, IGeminiApiClient geminiClient, IMicrophoneRecorder microphoneRecorder, ITextToSpeech textToSpeech, IWakeWordListener wakeWordListener)
    {
        _settingsStore = settingsStore;
        _geminiConfiguration = geminiConfiguration;
        _geminiClient = geminiClient;
        _microphoneRecorder = microphoneRecorder;
        _textToSpeech = textToSpeech;
        _wakeWordListener = wakeWordListener;
        InitializeComponent();
        _trayIcon = CreateTrayIcon();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settingsLoaded = false;
        try
        {
            var settings = await _settingsStore.LoadAsync();
            LaunchAtStartupCheckBox.IsChecked = settings.LaunchAtStartup;
            StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
            MicrophoneCheckBox.IsChecked = false;
            WakeWordCheckBox.IsChecked = settings.WakeWordEnabled;
            SpeechLanguageComboBox.SelectedIndex = settings.SpeechLanguage switch
            {
                "en-IN" => 1,
                "hi-IN" => 2,
                _ => 0
            };
            try { NeroStartupRegistration.SetEnabled(settings.LaunchAtStartup); }
            catch (Exception ex) { SaveStatusText.Text = $"Startup registration unavailable: {ex.Message}"; }
            _settingsLoaded = true;
            RefreshGeminiStatus();
            if (settings.WakeWordEnabled && _geminiConfiguration.IsConfigured) StartWakeWordListener();
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
        WakeWordCheckBox.IsEnabled = configured && _voiceRecordingTask is null;
        StartVoiceRecordingButton.IsEnabled = configured && _wakeWordTask is null && _voiceRecordingTask is null;
        if (!configured && _wakeWordTask is not null) StopWakeWordListener();
        else if (configured && _settingsLoaded && WakeWordCheckBox.IsChecked == true && _wakeWordTask is null) StartWakeWordListener();
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
        StopWakeWordListener();
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

        if (_wakeWordTask is not null)
        {
            VoiceStatusText.Text = "Disable Hey NERO listening before starting manual recording.";
            return;
        }

        try
        {
            _voiceRecordingCancellation = new CancellationTokenSource();
            _voiceRecordingTask = _microphoneRecorder.RecordWaveUntilCancelledAsync(_voiceRecordingCancellation.Token);
            StartVoiceRecordingButton.IsEnabled = false;
            StopVoiceRecordingButton.IsEnabled = true;
            WakeWordCheckBox.IsEnabled = false;
            MicrophoneConnectionStateText.Text = "Recording locally";
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
                VoiceStatusText.Text = "Voice request completed.";
            }
            catch (Exception speechError)
            {
                SpeechStatusText.Text = $"Text reply is ready, but speech output is unavailable: {speechError.Message}";
                VoiceStatusText.Text = "Voice request completed with text response.";
            }
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
            StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured && _wakeWordTask is null;
            StopVoiceRecordingButton.IsEnabled = false;
            WakeWordCheckBox.IsEnabled = _geminiConfiguration.IsConfigured;
            MicrophoneConnectionStateText.Text = _wakeWordTask is null ? "Idle — no audio captured" : "Local wake-word listening";
        }
    }

    private void CancelVoiceRecording()
    {
        var cancellation = _voiceRecordingCancellation;
        var recording = _voiceRecordingTask;
        _voiceRecordingCancellation = null;
        _voiceRecordingTask = null;
        if (cancellation is null) return;

        StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured && _wakeWordTask is null;
        StopVoiceRecordingButton.IsEnabled = false;
        WakeWordCheckBox.IsEnabled = _geminiConfiguration.IsConfigured;
        MicrophoneConnectionStateText.Text = _wakeWordTask is null ? "Idle — no audio captured" : "Local wake-word listening";
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


    private async void WakeWordToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsLoaded) return;
        var enabled = WakeWordCheckBox.IsChecked == true;
        if (enabled) StartWakeWordListener();
        else StopWakeWordListener();

        try
        {
            var settings = await _settingsStore.LoadAsync();
            await _settingsStore.SaveAsync(settings with { WakeWordEnabled = enabled });
        }
        catch (Exception ex)
        {
            WakeWordStatusText.Text = $"Could not save wake-word preference: {ex.Message}";
        }
    }

    private void StartWakeWordListener()
    {
        if (_voiceRecordingTask is not null)
        {
            WakeWordCheckBox.IsChecked = false;
            WakeWordStatusText.Text = "Stop manual recording before enabling Hey NERO.";
            return;
        }
        if (!_geminiConfiguration.IsConfigured)
        {
            WakeWordCheckBox.IsChecked = false;
            WakeWordStatusText.Text = "Configure Gemini before enabling Hey NERO.";
            return;
        }
        if (_wakeWordTask is not null) return;

        var cancellation = new CancellationTokenSource();
        _wakeWordCancellation = cancellation;
        WakeWordStatusText.Text = "Preparing local wake-word model. First setup downloads about 40 MB.";
        WakeWordCheckBox.IsEnabled = false;
        StartVoiceRecordingButton.IsEnabled = false;
        _trayIcon.Text = "NERO — local wake word";
        var task = RunWakeWordLoopAsync(cancellation.Token);
        _wakeWordTask = task;
        _ = ObserveWakeWordTaskAsync(task, cancellation);
    }

    private async Task RunWakeWordLoopAsync(CancellationToken cancellationToken)
    {
        await _wakeWordListener.PrepareAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            WakeWordStatusText.Text = "Hey NERO is listening locally. Audio is not sent to the cloud until after detection.";
            MicrophoneConnectionStateText.Text = "Local wake-word listening";
            WakeWordCheckBox.IsEnabled = true;
        });
        await _wakeWordListener.ListenAsync(HandleWakeWordDetectedAsync, cancellationToken).ConfigureAwait(false);
    }

    private async Task ObserveWakeWordTaskAsync(Task task, CancellationTokenSource cancellation)
    {
        Exception? failure = null;
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            var wasCancelled = cancellation.IsCancellationRequested;
            await Dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_wakeWordTask, task)) return;
                _wakeWordTask = null;
                _wakeWordCancellation = null;
                _trayIcon.Text = "NERO";
                StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured;
                WakeWordCheckBox.IsEnabled = _geminiConfiguration.IsConfigured;
                MicrophoneConnectionStateText.Text = "Idle — no audio captured";
                cancellation.Dispose();
                if (failure is not null)
                {
                    WakeWordStatusText.Text = $"Hey NERO stopped: {failure.Message}";
                    WakeWordCheckBox.IsChecked = false;
                }
                else if (wasCancelled)
                {
                    WakeWordStatusText.Text = "Hey NERO listening is off.";
        MicrophoneConnectionStateText.Text = "Idle — no audio captured";
        StartVoiceRecordingButton.IsEnabled = _geminiConfiguration.IsConfigured;
        WakeWordCheckBox.IsEnabled = _geminiConfiguration.IsConfigured;
                }
            });
        }
    }

    private void StopWakeWordListener()
    {
        var cancellation = _wakeWordCancellation;
        var task = _wakeWordTask;
        _wakeWordCancellation = null;
        _wakeWordTask = null;
        _trayIcon.Text = "NERO";
        WakeWordStatusText.Text = "Hey NERO listening is off.";
        if (cancellation is null) return;

        cancellation.Cancel();
        if (task is null)
        {
            cancellation.Dispose();
            return;
        }

        _ = task.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleWakeWordDetectedAsync(CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            ShowFromTray();
            VoiceStatusText.Text = "Hey NERO detected. Speak your command now…";
            MicrophoneConnectionStateText.Text = "Recording command locally";
            VoiceTranscriptText.Text = string.Empty;
        });

        try
        {
            var audio = await _microphoneRecorder.RecordCommandUntilSilenceAsync(cancellationToken).ConfigureAwait(false);
            if (audio.Length <= 44)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    VoiceStatusText.Text = "I heard Hey NERO but no command. Say Hey NERO and then speak after the prompt.";
                    MicrophoneConnectionStateText.Text = "Local wake-word listening";
                });
                return;
            }

            var apiKey = _geminiConfiguration.GetApiKeyForRequest();
            await Dispatcher.InvokeAsync(() =>
            {
                VoiceStatusText.Text = "Transcribing your voice command…";
                MicrophoneConnectionStateText.Text = "Idle — processing command";
            });
            var transcript = await _geminiClient.TranscribeAudioAsync(apiKey, audio, cancellationToken).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() =>
            {
                VoiceTranscriptText.Text = transcript;
                VoiceStatusText.Text = "Thinking…";
            });

            var answer = await _geminiClient.GenerateTextAsync(
                apiKey,
                "Respond helpfully and naturally to this spoken user request. The transcript may contain Hindi, Hinglish, or English. Do not claim to have performed computer actions; just answer the request.\n\nUser said: " + transcript,
                cancellationToken).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() =>
            {
                GeminiResponseText.Text = answer;
                VoiceStatusText.Text = "Speaking response…";
            });

            try
            {
                await _textToSpeech.SpeakAsync(answer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception speechError)
            {
                await Dispatcher.InvokeAsync(() => SpeechStatusText.Text = $"Speech output unavailable: {speechError.Message}");
            }

            await Dispatcher.InvokeAsync(() =>
            {
                VoiceStatusText.Text = "Ready. Say Hey NERO again.";
                MicrophoneConnectionStateText.Text = "Local wake-word listening";
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                VoiceStatusText.Text = $"Voice command failed: {ex.Message}";
                MicrophoneConnectionStateText.Text = "Local wake-word listening";
            });
        }
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
        StopWakeWordListener();
        WakeWordCheckBox.IsChecked = false;
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
                WakeWordEnabled = WakeWordCheckBox.IsChecked == true,
                SpeechLanguage = language
            };
            NeroStartupRegistration.SetEnabled(settings.LaunchAtStartup);
            await _settingsStore.SaveAsync(settings);
            if (settings.WakeWordEnabled) StartWakeWordListener(); else StopWakeWordListener();
            SaveStatusText.Text = "Settings saved on this device.";
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = $"Could not save settings: {ex.Message}";
        }
    }
}
