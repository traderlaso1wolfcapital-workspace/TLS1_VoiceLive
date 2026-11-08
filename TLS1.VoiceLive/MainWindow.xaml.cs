using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using TLS1.VoiceLive.Models;
using TLS1.VoiceLive.ViewModels;

namespace TLS1.VoiceLive;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private string? _selectedAudioFilePath;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        UpdateAiReplyButtonVisual(_viewModel.IsAiReplyEnabled);
        UpdateMuteButtonVisual(_viewModel.IsMuted);
        SwitchTab(0);
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsAiReplyEnabled))
            {
                Dispatcher.Invoke(() => UpdateAiReplyButtonVisual(_viewModel.IsAiReplyEnabled));
            }
            else if (e.PropertyName == nameof(MainViewModel.IsMuted))
            {
                Dispatcher.Invoke(() => UpdateMuteButtonVisual(_viewModel.IsMuted));
            }
        };
    }

    private void CbPlatform_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is PlatformInfo platform)
        {
            var currentUrl = _viewModel.LivestreamUrl?.Trim() ?? "";
            var suggestion = platform.Id switch
            {
                "twitch" => "wss://irc-ws.chat.twitch.tv:443",
                "youtube" => "wss://live-chat.youtube.com",
                "tiktok" => "wss://webcast.tiktok.com/webcast/ws",
                "okx" => "wss://ws.okx.com:8443/ws/v5/public",
                "demo" => "",
                _ => currentUrl
            };
            if (string.IsNullOrEmpty(currentUrl) && !string.IsNullOrEmpty(suggestion))
                _viewModel.LivestreamUrl = suggestion;
        }
    }

    private void TxtLivestreamUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        var url = _viewModel.LivestreamUrl?.Trim();
        if (string.IsNullOrEmpty(url) || url.StartsWith("wss://") || url.StartsWith("ws://"))
            return;

        var (platformId, channel) = ParseLivestreamUrl(url);
        if (platformId == null) return;

        foreach (PlatformInfo item in cbPlatform.Items)
        {
            if (item.Id == platformId) { cbPlatform.SelectedItem = item; break; }
        }

        if (!string.IsNullOrEmpty(channel))
            _viewModel.ChannelName = channel;
    }

    private static (string?, string?) ParseLivestreamUrl(string url)
    {
        var tw = Regex.Match(url, @"twitch\.tv/([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        if (tw.Success) return ("twitch", tw.Groups[1].Value);

        // YouTube: watch?v=, live_chat?is_popout=1&v=, youtu.be/
        var yt = Regex.Match(url, @"(?:youtube\.com/(?:watch|live_chat)\?.*v=|youtu\.be/)([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
        if (yt.Success) return ("youtube", yt.Groups[1].Value);

        var tk = Regex.Match(url, @"tiktok\.com/@([a-zA-Z0-9_.]+)/live", RegexOptions.IgnoreCase);
        if (tk.Success) return ("tiktok", tk.Groups[1].Value);

        var fb = Regex.Match(url, @"facebook\.com/([a-zA-Z0-9.]+)/videos", RegexOptions.IgnoreCase);
        if (fb.Success) return ("facebook", fb.Groups[1].Value);

        return (null, null);
    }

    private void BtnBrowseAudio_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file MP3 giọng nói để clone",
            Filter = "Audio Files (*.mp3;*.wav;*.ogg;*.m4a)|*.mp3;*.wav;*.ogg;*.m4a|All Files (*.*)|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            _selectedAudioFilePath = dialog.FileName;
            txtCloneFilePath.Text = dialog.FileName;
            btnCloneVoice.IsEnabled = true;
        }
    }

    private async void BtnCloneVoice_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAudioFilePath))
        {
            MessageBox.Show("Vui lòng chọn file MP3 trước.", "Thiếu file", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var apiKey = txtCloneApiKey.Text?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show("Vui lòng tích 🔑 Nhập API Key.\n\nLấy key tại: elevenlabs.io → Profile → API Key\n(10k ký tự free/tháng)\n💡 Free: Coqui TTS, Piper TTS, RVC", "Thiếu API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            chkShowApiKey.IsChecked = true;
            return;
        }
        btnCloneVoice.IsEnabled = false; btnCloneVoice.Content = "⏳..."; txtCloneStatus.Text = "🔄 Đang clone...";
        try
        {
            var engine = _viewModel.GetTtsEngine();
            if (engine == null) { txtCloneStatus.Text = "❌ Engine lỗi"; return; }
            var voiceId = await engine.CloneVoiceFromAudioAsync(_selectedAudioFilePath, $"Clone_{System.DateTime.Now:yyyyMMdd_HHmmss}", apiKey);
            if (!string.IsNullOrEmpty(voiceId))
            { txtCloneStatus.Text = $"✅ Clone: {voiceId[..12]}..."; MessageBox.Show($"Clone thành công!\nVoice ID: {voiceId}", "Thành công"); }
            else { txtCloneStatus.Text = "❌ Clone thất bại"; }
        }
        finally { btnCloneVoice.IsEnabled = true; btnCloneVoice.Content = "🔄 Clone"; }
    }

    private void ChkShowApiKey_Changed(object sender, RoutedEventArgs e)
        => txtCloneApiKey.Visibility = chkShowApiKey.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("📖 HƯỚNG DẪN LẤY WEBSOCKET / API\n\n🟣 TWITCH: Kênh: tên kênh | URL: wss://irc-ws.chat.twitch.tv:443 | Cần OAuth token\n🔴 YOUTUBE: Kênh: Video ID | URL: wss://live-chat.youtube.com | Hoặc dán link popup chat\n⚫ TIKTOK: Kênh: Room ID | URL: wss://webcast.tiktok.com/webcast/ws\n⚪ OKX: URL: wss://ws.okx.com:8443/ws/v5/public | Không cần Kênh\n🔗 WEBSOCKET: Dán URL wss:// bất kỳ\n\n⚠ Dán link web xem video KHÔNG lấy được bình luận!\nCần WebSocket URL hoặc link popup chat.", "Hướng dẫn", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is LiveSource source)
            _viewModel.LiveSources.Remove(source);
    }

    /// <summary>
    /// Thêm slang vào từ điển khi người dùng nhấn ➕.
    /// </summary>
    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMuted = !_viewModel.IsMuted;
        UpdateMuteButtonVisual(_viewModel.IsMuted);
    }

    private void UpdateMuteButtonVisual(bool isMuted)
    {
        if (btnMute == null) return;
        btnMute.Content = isMuted ? "🔇" : "🔊";
        btnMute.Background = isMuted
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0x2F, 0x42));
        txtCloneStatus.Text = isMuted ? "🔇 Đã tắt âm thanh" : "🔊 Đã bật âm thanh";
    }

    private void BtnToggleAiReply_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsAiReplyEnabled = !_viewModel.IsAiReplyEnabled;
        UpdateAiReplyButtonVisual(_viewModel.IsAiReplyEnabled);
    }

    private void UpdateAiReplyButtonVisual(bool enabled)
    {
        if (btnAiReply == null || txtAiIcon == null || txtAiLabel == null) return;

        if (enabled)
        {
            btnAiReply.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x2E, 0x0F));
            btnAiReply.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
            btnAiReply.BorderThickness = new Thickness(1.5);
            txtAiIcon.Text = "🤖";
            txtAiIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
            txtAiLabel.Text = "Trả lời AI";
            txtAiLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
        }
        else
        {
            btnAiReply.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0x2F, 0x42));
            btnAiReply.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x45, 0x5E));
            btnAiReply.BorderThickness = new Thickness(1);
            txtAiIcon.Text = "🤖";
            txtAiIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
            txtAiLabel.Text = "Trả lời AI";
            txtAiLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
        }
    }

    private void BtnTabChat_Click(object sender, RoutedEventArgs e) => SwitchTab(0);

    private void BtnTabSettings_Click(object sender, RoutedEventArgs e) => SwitchTab(1);

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var isSettingsActive = viewSettings.Visibility == Visibility.Visible;
        SwitchTab(isSettingsActive ? 0 : 1);
    }

    public void SwitchTab(int tabIndex)
    {
        if (viewChatLog == null || viewSettings == null) return;

        var activeBorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x45, 0x5E));
        var activeBgBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x23, 0x33));
        var activeFgBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
        var inactiveFgBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

        if (tabIndex == 0)
        {
            viewChatLog.Visibility = Visibility.Visible;
            viewSettings.Visibility = Visibility.Collapsed;

            btnTabChat.Background = activeBgBrush;
            btnTabChat.BorderBrush = activeBorderBrush;
            btnTabChat.BorderThickness = new Thickness(1, 1, 1, 0);
            txtTabChatLabel.Foreground = activeFgBrush;
            txtTabChatLabel.FontWeight = FontWeights.Bold;

            btnTabSettings.Background = System.Windows.Media.Brushes.Transparent;
            btnTabSettings.BorderThickness = new Thickness(0);
            txtTabSettingsLabel.Foreground = inactiveFgBrush;
            txtTabSettingsLabel.FontWeight = FontWeights.SemiBold;

            UpdateSettingsButtonVisual(false);
        }
        else
        {
            viewChatLog.Visibility = Visibility.Collapsed;
            viewSettings.Visibility = Visibility.Visible;

            btnTabChat.Background = System.Windows.Media.Brushes.Transparent;
            btnTabChat.BorderThickness = new Thickness(0);
            txtTabChatLabel.Foreground = inactiveFgBrush;
            txtTabChatLabel.FontWeight = FontWeights.SemiBold;

            btnTabSettings.Background = activeBgBrush;
            btnTabSettings.BorderBrush = activeBorderBrush;
            btnTabSettings.BorderThickness = new Thickness(1, 1, 1, 0);
            txtTabSettingsLabel.Foreground = activeFgBrush;
            txtTabSettingsLabel.FontWeight = FontWeights.Bold;

            UpdateSettingsButtonVisual(true);
        }
    }

    private void UpdateSettingsButtonVisual(bool isOpen)
    {
        if (btnSettings == null || txtSettingsLabel == null) return;
        if (isOpen)
        {
            btnSettings.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x2E, 0x0F));
            btnSettings.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
            btnSettings.BorderThickness = new Thickness(1.5);
            txtSettingsLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
        }
        else
        {
            btnSettings.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0x2F, 0x42));
            btnSettings.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x45, 0x5E));
            btnSettings.BorderThickness = new Thickness(1);
            txtSettingsLabel.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
        }
    }

    private void BtnAddSlang_Click(object sender, RoutedEventArgs e)
    {
        var key = txtSlangKey.Text?.Trim();
        var value = txtSlangValue.Text?.Trim();
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
        {
            TLS1.AI.Engines.EdgeTtsEngine.AddSlang(key, value);
            txtSlangKey.Text = "";
            txtSlangValue.Text = "";
            txtCloneStatus.Text = $"✅ Đã lưu slang: {key} = {value}";
        }
    }

    private void BtnClearClone_Click(object sender, RoutedEventArgs e)
    {
        _selectedAudioFilePath = null;
        txtCloneFilePath.Text = "Chưa chọn file";
        btnCloneVoice.IsEnabled = false;
        _viewModel.GetTtsEngine()?.ClearClonedVoice();
        txtCloneStatus.Text = "🗑 Đã xóa clone, dùng Edge TTS";
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && !b;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && !b;
}