using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Options;
using TLS1.Core.Configuration;
using TLS1.Core.Interfaces.AI;
using TLS1.Core.Interfaces.Chat;
using TLS1.Core.Models.Chat;
using TLS1.VoiceLive.Models;

namespace TLS1.VoiceLive.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IChatAggregator _chatAggregator;
    private readonly ITtsEngine _ttsEngine;
    private readonly IOptions<TtsOptions> _ttsOptions;
    private readonly Dispatcher _dispatcher;

    private bool _isRunning;
    private string _statusText = "🟢 Sẵn sàng";
    private string _connectedPlatformsText = "⚪ Chưa kết nối";
    private string _selectedVoiceId = "vi-VN-Neural2-A";
    private string _prefixTemplate = "[{Platform}] {Username} nói: ";
    private int _messageCount;
    private PlatformInfo? _selectedPlatform;
    private string _livestreamUrl = "";
    private string _channelName = "";
    private CancellationTokenSource? _demoCts;
    private readonly Dictionary<LiveSource, TLS1.Chat.Providers.BrowserScraperProvider> _scrapers = new();
    private TLS1.Chat.Providers.TikTokConsoleBridge? _tikTokBridge;

    public ObservableCollection<ChatLogEntry> ChatLog { get; } = new();
    public ObservableCollection<VoiceInfo> AvailableVoices { get; } = new();
    public ObservableCollection<PlatformInfo> AvailablePlatforms { get; } = new();
    public ObservableCollection<LiveSource> LiveSources { get; } = new();

    public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public string ConnectedPlatformsText { get => _connectedPlatformsText; set { _connectedPlatformsText = value; OnPropertyChanged(); } }
    public string SelectedVoiceId
    {
        get => _selectedVoiceId;
        set
        {
            if (_selectedVoiceId == value) return;
            _selectedVoiceId = value;
            OnPropertyChanged();
            _ttsOptions.Value.VoiceId = value;

            if (_ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts)
            {
                edgeTts.SetVoice(value ?? "");
            }

            var voiceObj = AvailableVoices.FirstOrDefault(v => v.Id == value);
            var voiceName = voiceObj?.Name ?? value;
            
            // Chỉ ghi nhận thông báo chuyển giọng vào CHAT LOG, không phát ra tiếng
            AddLogEntry("HỆ THỐNG", $"📢 Đã chuyển sang giọng đọc: {voiceName}", "SYSTEM");
        }
    }
    public string PrefixTemplate { get => _prefixTemplate; set { _prefixTemplate = value; OnPropertyChanged(); } }
    public int MessageCount { get => _messageCount; set { _messageCount = value; OnPropertyChanged(); } }
    public PlatformInfo? SelectedPlatform { get => _selectedPlatform; set { _selectedPlatform = value; OnPropertyChanged(); } }
    public string LivestreamUrl { get => _livestreamUrl; set { _livestreamUrl = value; OnPropertyChanged(); } }
    public string ChannelName { get => _channelName; set { _channelName = value; OnPropertyChanged(); } }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand AddSourceCommand { get; }
    public ICommand RemoveSourceCommand { get; }

    public TLS1.AI.Engines.EdgeTtsEngine? GetTtsEngine()
        => _ttsEngine as TLS1.AI.Engines.EdgeTtsEngine;

    public MainViewModel(IChatAggregator chatAggregator, ITtsEngine ttsEngine, IOptions<TtsOptions> ttsOptions)
    {
        _chatAggregator = chatAggregator;
        _ttsEngine = ttsEngine;
        _ttsOptions = ttsOptions;
        _dispatcher = Dispatcher.CurrentDispatcher;

        LoadVoices();

        AvailablePlatforms = new ObservableCollection<PlatformInfo>
        {
            new() { Id = "demo", Name = "🟢 Demo", Description = "Bình luận mẫu crypto" },
            new() { Id = "twitch", Name = "🟣 Twitch", Description = "Chat Twitch IRC" },
            new() { Id = "youtube", Name = "🔴 YouTube", Description = "Live chat YouTube" },
            new() { Id = "tiktok", Name = "⚫ TikTok", Description = "TikTok LIVE WebSocket" },
            new() { Id = "facebook", Name = "🔵 Facebook", Description = "Facebook Live" },
            new() { Id = "okx", Name = "⚪ OKX", Description = "Tín hiệu OKX" },
            new() { Id = "websocket", Name = "🔗 WebSocket", Description = "URL tùy chỉnh" },
        };
        SelectedPlatform = AvailablePlatforms[0];
        // Mặc định không có nguồn nào, user tự thêm

        StartCommand = new RelayCommand(async () => await StartAsync(), () => !IsRunning);
        StopCommand = new RelayCommand(async () => await StopAsync(), () => IsRunning);
        ClearLogCommand = new RelayCommand(() => ChatLog.Clear());
        AddSourceCommand = new RelayCommand(AddSource);
        RemoveSourceCommand = new RelayCommand<LiveSource>(RemoveSource);

        _chatAggregator.OnMessageAggregated += OnChatMessageReceived;
        _chatAggregator.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void AddSource()
    {
        if (_selectedPlatform == null) return;
        var newSource = new LiveSource
        {
            PlatformId = _selectedPlatform.Id,
            PlatformName = _selectedPlatform.Name,
            ChannelName = _channelName,
            Url = _livestreamUrl
        };
        LiveSources.Add(newSource);
        _channelName = "";
        _livestreamUrl = "";
        UpdateSourceCount();

        if (IsRunning && newSource.PlatformId != "demo")
            StartScraperForSource(newSource);
    }

    /// <summary>Xóa nguồn + dừng scraper của nguồn đó ngay nếu đang chạy.</summary>
    private void RemoveSource(LiveSource source)
    {
        if (_scrapers.TryGetValue(source, out var scraper))
        {
            scraper.Stop();
            _scrapers.Remove(source);
        }
        LiveSources.Remove(source);
        UpdateSourceCount();
    }

    private void UpdateSourceCount()
    {
        ConnectedPlatformsText = LiveSources.Count == 0 ? "⚪ Chưa có nguồn" : $"⚪ {LiveSources.Count} nguồn";
    }

    private void LoadVoices()
    {
        foreach (var v in new VoiceInfo[]
        {
            new() { Id = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ - Tiếng Việt)", Gender = "Female" },
            new() { Id = "vi-VN-NamMinhNeural", Name = "Nam Minh (Nam - Tiếng Việt)", Gender = "Male" },
        }) AvailableVoices.Add(v);

        // Mặc định luôn là Giọng Nữ (Hoài My) khi mở app lên
        SelectedVoiceId = "vi-VN-HoaiMyNeural";
    }

    private async Task StartAsync()
    {
        if (LiveSources.Count == 0)
        {
            AddLogEntry("HỆ THỐNG", "⚠ Chưa có nguồn nào! Nhấn + Thêm nguồn.", "SYSTEM");
            return;
        }

        IsRunning = true;
        StatusText = "🟡 Đang chạy...";

        var demoSources = LiveSources.Where(s => s.PlatformId == "demo").ToList();
        var realSources = LiveSources.Where(s => s.PlatformId != "demo").ToList();

        if (demoSources.Any()) _ = RunDemoModeAsync();

        foreach (var source in realSources)
            StartScraperForSource(source);

        UpdateSourceCount();
    }

    private string FormatSpeechText(string platform, string username, string content)
    {
        var text = content ?? "";

        // Quy đổi ký tự tiền tệ và ký tự đặc biệt phổ biến sang chữ tiếng Việt
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)\s*\$", "$1 đô");
        text = text.Replace("$", " đô ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)\s*€", "$1 ơ-rô");
        text = text.Replace("€", " ơ-rô ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)\s*£", "$1 bảng");
        text = text.Replace("£", " bảng ");
        text = text.Replace("%", " phần trăm ");

        // Quy đổi các từ viết tắt phổ biến gaming / livestream
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\bacc\b", "ạc", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\bcmt\b", "comment", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\bib\b", "inbox", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(PrefixTemplate))
            return $"{username} nói là: {text}";

        var prefix = PrefixTemplate.Replace("{Platform}", platform ?? "")
                                   .Replace("{Username}", username ?? "");

        // Nếu prefix chưa kết thúc bằng khoảng trắng hoặc dấu hai chấm thì thêm khoảng trắng hợp lý
        if (!prefix.EndsWith(" ") && !prefix.EndsWith(":"))
            prefix += " ";

        return $"{prefix}{text}";
    }

    private void StartScraperForSource(LiveSource source)
    {
        var scraper = new TLS1.Chat.Providers.BrowserScraperProvider();
        scraper.OnComment += (msg) =>
        {
            if (!LiveSources.Contains(source)) return; // Nguồn đã bị xóa → bỏ qua
            _dispatcher.Invoke(() =>
            {
                ChatLog.Add(new ChatLogEntry { Timestamp = msg.Timestamp, Platform = msg.Platform, Username = msg.Username, Content = msg.Content });
                MessageCount++;
                while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
            });
            if (_ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts)
                _ = edgeTts.SpeakCommentAsync(msg.Username, msg.Content);
            else
            {
                var textToSpeak = FormatSpeechText(msg.Platform, msg.Username, msg.Content);
                _ = _ttsEngine.SpeakAsync(textToSpeak);
            }
        };
        scraper.OnStatus += (status) =>
        {
            _dispatcher.Invoke(() =>
            {
                // Hiển thị status kèm tên nguồn
                var label = $"[{source.PlatformName}] {(string.IsNullOrEmpty(source.ChannelName) ? "" : source.ChannelName)}";
                ConnectedPlatformsText = IsRunning ? $"{label}: {status}" : status;
                if (status.Contains("❌") || status.Contains("error", StringComparison.OrdinalIgnoreCase))
                    source.Error = status.Replace("❌", "").Trim();
            });
        };

        _ = scraper.StartAsync(source.PlatformId, source.Url, source.ChannelName);
        _scrapers[source] = scraper;
    }

    private async Task StopAsync()
    {
        IsRunning = false;
        _demoCts?.Cancel();
        (_ttsEngine as TLS1.AI.Engines.EdgeTtsEngine)?.CancelAll();
        _tikTokBridge?.Stop();
        _tikTokBridge?.Dispose();
        _tikTokBridge = null;
        foreach (var kv in _scrapers) kv.Value.Stop();
        _scrapers.Clear();
        try { await _chatAggregator.DisconnectAllAsync(); } catch { }
        StatusText = "🟢 Đã dừng";
        ConnectedPlatformsText = "⚪ Đã ngắt kết nối";
    }

    private async Task RunDemoModeAsync()
    {
        _demoCts = new CancellationTokenSource();
        var token = _demoCts.Token;
        var rnd = new Random();
        var names = new[] { "CryptoKing", "TraderVN", "Hodler999", "BitcoinMaxi", "AltcoinHunter", "DefiQueen" };
        var messages = new[]
        {
            "Mua BTC đi ae ơi! Sắp pump mạnh", "Sell hết đi, market đỏ rồi",
            "ETH sắp lên 5000$, tin tôi đi", "Ai đang hold SOL không? Tôi vừa mua thêm",
            "Thanh lý hết rồi, Mất 2000$", "Vừa mua thêm 1000 USDT để DCA",
            "Khi nào BTC về 70k vậy ae?", "Altcoin mùa tới rồi! Chuẩn bị đi",
            "Tin xấu từ SEC, cẩn thận nhé", "Hold hay bán đây ae? Khó quá",
            "Vào lệnh LONG BTC 50x, ăn dày", "SHORT ETH 20x, đang có tín hiệu",
            "DCA BTC mỗi ngày, chiến lược dài hạn", "NFT sắp comeback, mua ngay!",
            "GameFi hệ TON đang hot lắm",
        };

        try
        {
            while (!token.IsCancellationRequested && IsRunning)
            {
                var username = names[rnd.Next(names.Length)];
                var message = messages[rnd.Next(messages.Length)];
                _dispatcher.Invoke(() =>
                {
                    ChatLog.Add(new ChatLogEntry { Timestamp = DateTime.Now, Platform = "Demo", Username = username, Content = message });
                    MessageCount++;
                    while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
                });
                if (_ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts)
                    await edgeTts.SpeakCommentAsync(username, message);
                else
                {
                    var textToSpeak = FormatSpeechText("Demo", username, message);
                    await _ttsEngine.SpeakAsync(textToSpeak);
                }
                try { await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3, 8)), token); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async void OnChatMessageReceived(ChatMessage msg)
    {
        _dispatcher.Invoke(() =>
        {
            ChatLog.Add(new ChatLogEntry { Timestamp = msg.Timestamp, Platform = msg.Platform, Username = msg.Username, Content = msg.Content });
            MessageCount++;
            while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
        });
        var textToSpeak = FormatSpeechText(msg.Platform, msg.Username, msg.Content);
        await _ttsEngine.SpeakAsync(textToSpeak);
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        _dispatcher.Invoke(() =>
        {
            ConnectedPlatformsText = connected ? $"✅ Đã kết nối: {string.Join(", ", _chatAggregator.ConnectedPlatforms)}" : "⚪ Đã ngắt";
        });
    }

    private void AddLogEntry(string username, string content, string platform)
    {
        _dispatcher.Invoke(() => ChatLog.Add(new ChatLogEntry { Timestamp = DateTime.Now, Platform = platform, Username = username, Content = content }));
    }

    /// Bắt đầu TikTok Console Bridge (HTTP server nhận comment từ Chrome Console).
    private void StartTikTokBridge(LiveSource source)
    {
        var bridge = new TLS1.Chat.Providers.TikTokConsoleBridge(9999);
        bridge.OnComment += (msg) =>
        {
            _dispatcher.Invoke(() =>
            {
                ChatLog.Add(new ChatLogEntry { Timestamp = msg.Timestamp, Platform = msg.Platform, Username = msg.Username, Content = msg.Content });
                MessageCount++;
                while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
            });
            if (_ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts)
                _ = edgeTts.SpeakCommentAsync(msg.Username, msg.Content);
            else
            {
                var textToSpeak = FormatSpeechText(msg.Platform, msg.Username, msg.Content);
                _ = _ttsEngine.SpeakAsync(textToSpeak);
            }
        };
        bridge.OnStatus += (status) =>
        {
            _dispatcher.Invoke(() =>
            {
                ConnectedPlatformsText = IsRunning ? status : "";
                StatusText = status;
            });
        };

        _ = bridge.StartAsync();
        _tikTokBridge = bridge; // Lưu lại để StopAsync
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ChatLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{Platform}] {Username}: {Content}";
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => _canExecute?.Invoke() ?? true;
    public void Execute(object? p) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    public RelayCommand(Action<T> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add {} remove {} }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) { if (p is T t) _execute(t); }
}