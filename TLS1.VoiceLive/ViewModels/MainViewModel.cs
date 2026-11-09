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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
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
    private CancellationTokenSource? _liveSpeechCts;
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly Dictionary<LiveSource, TLS1.Chat.Providers.BrowserScraperProvider> _scrapers = new();
    private TLS1.Chat.Providers.TikTokConsoleBridge? _tikTokBridge;

    public ObservableCollection<ChatLogEntry> ChatLog { get; } = new();
    public ObservableCollection<VoiceInfo> AvailableVoices { get; } = new();
    public ObservableCollection<PlatformInfo> AvailablePlatforms { get; } = new();
    public ObservableCollection<LiveSource> LiveSources { get; } = new();

    private static readonly HttpClient _aiHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private bool _isAiReplyEnabled;
    private CancellationTokenSource? _aiReplyCts;
    private int _isProcessingAiReply = 0;
    private DateTime _lastAiReplyTime = DateTime.MinValue;
    private static readonly HashSet<string> _ignoredBotUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "nightbot", "streamlabs", "moobot", "streamelements", "tiktoklive", "system", "hệ thống", "bot"
    };
    private static readonly Regex _spamPattern = new(@"^[\s\p{P}\p{S}:;)(/\\=+\-_><~]+$", RegexOptions.Compiled);

    private string _aiEndpoint = "http://192.168.100.154:20128/v1/chat/completions";
    private string _aiApiKey = "sk-94f37e79b2d930fe-3qwzqr-6541de82";
    private string _aiModel = "main";
    private string _aiSystemPrompt = 
        "Bạn là trợ lý AI thông minh trong phòng livestream tiếng Việt. " +
        "Nhiệm vụ: Trả lời hoặc giao lưu ngắn gọn với khán giả cực kỳ tự nhiên, hóm hỉnh như người thật. " +
        "Quy tắc bắt buộc: " +
        "1. Trả lời CỰC KỲ NGẮN GỌN (chỉ đúng 1 câu, tối đa 15-20 từ). " +
        "2. Đi thẳng vào câu trả lời, TUYỆT ĐỐI KHÔNG mở đầu bằng 'Tôi là AI', 'AI nói', 'Chào bạn'. " +
        "3. Xưng hô tự nhiên (em/tui, gọi khán giả bằng tên hoặc bác). " +
        "4. Tuyệt đối không dùng emoji, icon, hay ký tự markdown (*, #, `) để giọng đọc phát âm mượt.";

    public bool IsAiReplyEnabled
    {
        get => _isAiReplyEnabled;
        set
        {
            if (_isAiReplyEnabled == value) return;
            _isAiReplyEnabled = value;
            OnPropertyChanged();

            if (value)
            {
                _aiReplyCts?.Cancel();
                _aiReplyCts?.Dispose();
                _aiReplyCts = new CancellationTokenSource();
                AddLogEntry("HỆ THỐNG", "🤖 Đã BẬT chế độ: Trả lời comment AI (9Router Mac Mini)", "SYSTEM");
            }
            else
            {
                // Dừng ngay lập tức toàn bộ tiến trình trả lời của AI
                _aiReplyCts?.Cancel();
                _aiReplyCts?.Dispose();
                _aiReplyCts = null;
                Interlocked.Exchange(ref _isProcessingAiReply, 0);
                AddLogEntry("HỆ THỐNG", "⚪ Đã TẮT chế độ: Trả lời comment AI", "SYSTEM");
            }
        }
    }

    public string AiEndpoint { get => _aiEndpoint; set { _aiEndpoint = value; OnPropertyChanged(); } }
    public string AiApiKey { get => _aiApiKey; set { _aiApiKey = value; OnPropertyChanged(); } }
    public string AiModel { get => _aiModel; set { _aiModel = value; OnPropertyChanged(); } }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            OnPropertyChanged();
            if (value)
            {
                Task.Run(() =>
                {
                    try { (_ttsEngine as TLS1.AI.Engines.EdgeTtsEngine)?.CancelAll(); } catch { }
                });
            }
        }
    }

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

    public MainViewModel(IChatAggregator chatAggregator, ITtsEngine ttsEngine, IOptions<TtsOptions> ttsOptions, IConfiguration? configuration = null)
    {
        _chatAggregator = chatAggregator;
        _ttsEngine = ttsEngine;
        _ttsOptions = ttsOptions;
        _dispatcher = Dispatcher.CurrentDispatcher;

        if (configuration != null)
        {
            _aiEndpoint = configuration["AiReply:Endpoint"] ?? _aiEndpoint;
            _aiApiKey = configuration["AiReply:ApiKey"] ?? _aiApiKey;
            _aiModel = configuration["AiReply:Model"] ?? _aiModel;
            _aiSystemPrompt = configuration["AiReply:SystemPrompt"] ?? _aiSystemPrompt;
            if (bool.TryParse(configuration["AiReply:Enabled"], out var enabled))
                _isAiReplyEnabled = enabled;
        }

        if (_isAiReplyEnabled)
        {
            _aiReplyCts = new CancellationTokenSource();
        }

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
        _ttsOptions.Value.Speed = 1.3;
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
        _liveSpeechCts?.Cancel();
        _liveSpeechCts?.Dispose();
        _liveSpeechCts = new CancellationTokenSource();

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

    private async Task SpeakChatMessageAsync(string platform, string username, string content)
    {
        if (IsMuted) return;
        try
        {
            // Tự động chuẩn hóa Slang & Teencode bằng 9Router AI trước khi phát âm
            var normalizedContent = await NormalizeSlangAndTeencodeAsync(content);

            if (_ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts)
                await edgeTts.SpeakCommentAsync(username, normalizedContent);
            else
            {
                var textToSpeak = FormatSpeechText(platform, username, normalizedContent);
                await _ttsEngine.SpeakAsync(textToSpeak);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi phát âm thanh bình luận");
        }
    }

    private async Task<string> NormalizeSlangAndTeencodeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Rút gọn các ký tự lặp kéo dài (vd: "đẹp quáaaaa" -> "đẹp quá", "helooooo" -> "heloo")
        var cleaned = Regex.Replace(text, @"(.)\1{3,}", "$1$1").Trim();

        // Nếu quá ngắn (< 3 ký tự) hoặc chỉ toàn số thì không cần gọi AI
        if (cleaned.Length < 3 || cleaned.All(char.IsDigit)) return cleaned;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500)); // Timeout 1.5s để không làm trễ live
            var normalized = await RequestSlangNormalizationFrom9RouterAsync(cleaned, cts.Token);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }
        catch
        {
            // Bỏ qua nếu lỗi mạng hoặc timeout, dùng text sạch
        }

        return cleaned;
    }

    private async Task<string?> RequestSlangNormalizationFrom9RouterAsync(string text, CancellationToken token)
    {
        try
        {
            var payload = new
            {
                model = string.IsNullOrWhiteSpace(_aiModel) ? "main" : _aiModel,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Bạn là bộ chuẩn hóa ngôn ngữ livestream tiếng Việt. " +
                                  "Nhiệm vụ: Chuyển các từ viết tắt, teencode, tiếng lóng mạng xã hội và crypto trong câu sang tiếng Việt tự nhiên đầy đủ để giọng đọc AI phát âm chuẩn nhất. " +
                                  "Quy tắc tối thượng: Chỉ trả về duy nhất câu đã chuẩn hóa, tuyệt đối không trả lời câu hỏi, không thêm bớt nội dung, không chào hỏi, không thêm icon."
                    },
                    new { role = "user", content = text }
                },
                max_tokens = 60,
                temperature = 0.2,
                stream = false
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _aiEndpoint);
            if (!string.IsNullOrWhiteSpace(_aiApiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {_aiApiKey}");
            }
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _aiHttpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return null;

            var respString = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(respString);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msgObj) && msgObj.TryGetProperty("content", out var contentElem))
                {
                    var result = contentElem.GetString();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        result = Regex.Replace(result, @"<think>.*?</think>", "", RegexOptions.Singleline);
                        result = result.Replace("*", "").Replace("#", "").Replace("`", "").Replace("\"", "").Trim();
                        return result;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Lỗi chuẩn hóa slang qua 9Router");
        }
        return null;
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

            _ = Task.Run(async () => await EnqueueCommentVoiceAsync(msg.Platform, msg.Username, msg.Content));
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

    private async Task EnqueueCommentVoiceAsync(string platform, string username, string content, CancellationToken customToken = default)
    {
        if (!IsRunning || IsMuted) return;

        var token = customToken != default ? customToken : (_liveSpeechCts?.Token ?? CancellationToken.None);
        if (token.IsCancellationRequested) return;

        try
        {
            await _speechLock.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (!IsRunning || IsMuted || token.IsCancellationRequested) return;

            // 1. Phát âm thanh đọc bình luận (đã có chuẩn hóa 9Router AI)
            await SpeakChatMessageAsync(platform, username, content);

            // 2. Nếu có bật Trả lời AI:
            if (IsAiReplyEnabled && ShouldAiReply(username, content))
            {
                // Delay đúng 5 giây trước khi AI trả lời theo yêu cầu của CEO
                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!IsRunning || !IsAiReplyEnabled || IsMuted || token.IsCancellationRequested) return;

                await ProcessAiReplyAsync(username, content);
            }

            // 3. Delay đúng 5 giây sau mỗi lần đọc xong trước khi nhường lượt cho cmt kế tiếp theo yêu cầu của CEO
            try
            {
                await Task.Delay(5000, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi trong chu trình đọc cmt và trả lời AI");
        }
        finally
        {
            try { _speechLock.Release(); } catch { }
        }
    }

    private async Task StopAsync()
    {
        try
        {
            IsRunning = false;

            try { _liveSpeechCts?.Cancel(); } catch { }
            try { _liveSpeechCts?.Dispose(); } catch { }
            _liveSpeechCts = null;

            try { _demoCts?.Cancel(); } catch { }
            try { _demoCts?.Dispose(); } catch { }
            _demoCts = null;

            try { _aiReplyCts?.Cancel(); } catch { }
            try { _aiReplyCts?.Dispose(); } catch { }
            _aiReplyCts = null;

            Interlocked.Exchange(ref _isProcessingAiReply, 0);

            try
            {
                (_ttsEngine as TLS1.AI.Engines.EdgeTtsEngine)?.CancelAll();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Lỗi khi hủy âm thanh TTS");
            }

            try
            {
                _tikTokBridge?.Stop();
                _tikTokBridge?.Dispose();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Lỗi khi dừng TikTok bridge");
            }
            _tikTokBridge = null;

            foreach (var kv in _scrapers.ToList())
            {
                try
                {
                    kv.Value.Stop();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Lỗi khi dừng scraper {Platform}", kv.Key.PlatformName);
                }
            }
            _scrapers.Clear();

            try
            {
                await _chatAggregator.DisconnectAllAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Lỗi khi ngắt kết nối ChatAggregator");
            }

            StatusText = "🟢 Đã dừng";
            ConnectedPlatformsText = "⚪ Đã ngắt kết nối";
            AddLogEntry("HỆ THỐNG", "⏹ Đã dừng đọc bình luận.", "SYSTEM");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi thực thi StopAsync");
        }
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

                if (!IsMuted)
                {
                    await EnqueueCommentVoiceAsync("Demo", username, message, token);
                }
                else
                {
                    try { await Task.Delay(5000, token); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnChatMessageReceived(ChatMessage msg)
    {
        _dispatcher.Invoke(() =>
        {
            ChatLog.Add(new ChatLogEntry { Timestamp = msg.Timestamp, Platform = msg.Platform, Username = msg.Username, Content = msg.Content });
            MessageCount++;
            while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
        });

        _ = Task.Run(async () => await EnqueueCommentVoiceAsync(msg.Platform, msg.Username, msg.Content));
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

            _ = Task.Run(async () => await EnqueueCommentVoiceAsync(msg.Platform, msg.Username, msg.Content));
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

    private bool ShouldAiReply(string username, string userComment)
    {
        if (string.IsNullOrWhiteSpace(userComment)) return false;

        // Bỏ qua bot livestream
        var cleanUser = username.Trim().TrimStart('@');
        if (_ignoredBotUsers.Contains(cleanUser)) return false;
        if (cleanUser.Contains("bot", StringComparison.OrdinalIgnoreCase)) return false;

        var trimmed = userComment.Trim();
        // Bỏ qua cmt quá ngắn (< 5 ký tự)
        if (trimmed.Length < 5) return false;

        // Bỏ qua cmt chỉ toàn emoji hoặc ký tự đặc biệt (vd: :))) @@ ??? !!!)
        if (_spamPattern.IsMatch(trimmed)) return false;

        // Bỏ qua chuỗi cười hoặc spam ký tự vô nghĩa lặp lại (vd: hahaha, kkkk, hehehe)
        if (Regex.IsMatch(trimmed, @"^(ha|he|hi|ho|k|a|h){3,}$", RegexOptions.IgnoreCase)) return false;

        // Cooldown tối thiểu 5 giây giữa 2 lần AI trả lời để không lấn át streamer
        if ((DateTime.Now - _lastAiReplyTime).TotalSeconds < 5) return false;

        return true;
    }

    private async Task ProcessAiReplyAsync(string username, string userComment)
    {
        if (!IsAiReplyEnabled) return;
        var cts = _aiReplyCts;
        if (cts == null || cts.IsCancellationRequested) return;

        if (!ShouldAiReply(username, userComment)) return;

        // Đảm bảo chỉ 1 câu trả lời AI được xử lý tại một thời điểm
        if (Interlocked.CompareExchange(ref _isProcessingAiReply, 1, 0) != 0) return;

        try
        {
            var reply = await RequestAiReplyFrom9RouterAsync(username, userComment, cts.Token);
            
            // Kiểm tra trạng thái hủy ngay sau khi nhận phản hồi (người dùng có thể vừa bỏ tích)
            if (!IsAiReplyEnabled || cts.IsCancellationRequested || string.IsNullOrWhiteSpace(reply))
                return;

            _lastAiReplyTime = DateTime.Now;

            _dispatcher.Invoke(() =>
            {
                ChatLog.Add(new ChatLogEntry
                {
                    Timestamp = DateTime.Now,
                    Platform = "AI",
                    Username = "🤖 AI Trả Lời",
                    Content = reply
                });
                MessageCount++;
                while (ChatLog.Count > 200) ChatLog.RemoveAt(0);
            });

            // Kiểm tra trạng thái hủy và mute lần nữa trước khi phát tiếng
            if (!IsAiReplyEnabled || cts.IsCancellationRequested || IsMuted) return;

            // Đọc trực tiếp câu trả lời của AI một cách tự nhiên, KHÔNG có tiền tố "AI nói là"
            await _ttsEngine.SpeakAsync(reply);
        }
        catch (OperationCanceledException)
        {
            // Bị hủy khi người dùng bỏ tích checkbox
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi xử lý trả lời comment AI từ 9Router");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessingAiReply, 0);
        }
    }

    private async Task<string?> RequestAiReplyFrom9RouterAsync(string username, string userComment, CancellationToken token)
    {
        try
        {
            var payload = new
            {
                model = string.IsNullOrWhiteSpace(_aiModel) ? "main" : _aiModel,
                messages = new object[]
                {
                    new { role = "system", content = _aiSystemPrompt },
                    new { role = "user", content = $"{username}: {userComment}" }
                },
                max_tokens = 60,
                temperature = 0.7,
                stream = false
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _aiEndpoint);
            if (!string.IsNullOrWhiteSpace(_aiApiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {_aiApiKey}");
            }
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _aiHttpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(token);
                Serilog.Log.Warning("9Router trả về mã lỗi {StatusCode}: {Error}", response.StatusCode, err);
                return null;
            }

            var respString = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(respString);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msgObj) && msgObj.TryGetProperty("content", out var contentElem))
                {
                    var text = contentElem.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.Singleline);
                        text = text.Replace("*", "").Replace("#", "").Replace("`", "").Trim();
                        return text;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Request bị hủy
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi kết nối tới 9Router Mac Mini");
        }
        return null;
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
    public void Execute(object? p)
    {
        try { _execute(); }
        catch (Exception ex) { Serilog.Log.Error(ex, "Lỗi thực thi RelayCommand"); }
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    public RelayCommand(Action<T> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add {} remove {} }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p)
    {
        try { if (p is T t) _execute(t); }
        catch (Exception ex) { Serilog.Log.Error(ex, "Lỗi thực thi RelayCommand<T>"); }
    }
}