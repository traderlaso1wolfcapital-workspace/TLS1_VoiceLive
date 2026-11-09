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

    // ── NGÔN NGỮ GIAO DIỆN & GIỌNG ĐỌC (LOCALIZATION) ──
    public ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new();
    private LanguageInfo _selectedLanguage = null!;
    public LanguageInfo SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value || value == null) return;
            _selectedLanguage = value;
            OnPropertyChanged();
            ApplyLanguage(value);
        }
    }

    private string _uiPlatformHeader = "NGUỒN LIVESTREAM — Thêm YouTube, TikTok, Twitch...";
    public string UiPlatformHeader { get => _uiPlatformHeader; set { _uiPlatformHeader = value; OnPropertyChanged(); } }

    private string _uiPlatformLabel = "Nền tảng:";
    public string UiPlatformLabel { get => _uiPlatformLabel; set { _uiPlatformLabel = value; OnPropertyChanged(); } }

    private string _uiChannelLabel = "Kênh / ID:";
    public string UiChannelLabel { get => _uiChannelLabel; set { _uiChannelLabel = value; OnPropertyChanged(); } }

    private string _uiUrlLabel = "URL / Link:";
    public string UiUrlLabel { get => _uiUrlLabel; set { _uiUrlLabel = value; OnPropertyChanged(); } }

    private string _uiAddSource = "＋  Thêm nguồn";
    public string UiAddSource { get => _uiAddSource; set { _uiAddSource = value; OnPropertyChanged(); } }

    private string _uiStart = "▶  BẮT ĐẦU";
    public string UiStart { get => _uiStart; set { _uiStart = value; OnPropertyChanged(); } }

    private string _uiStop = "⏹  DỪNG";
    public string UiStop { get => _uiStop; set { _uiStop = value; OnPropertyChanged(); } }

    private string _uiAiReply = "Trả lời AI";
    public string UiAiReply { get => _uiAiReply; set { _uiAiReply = value; OnPropertyChanged(); } }

    private string _uiClearLog = "🗑 Xóa log";
    public string UiClearLog { get => _uiClearLog; set { _uiClearLog = value; OnPropertyChanged(); } }

    private string _uiCommentsCountSuffix = " bình luận";
    public string UiCommentsCountSuffix { get => _uiCommentsCountSuffix; set { _uiCommentsCountSuffix = value; OnPropertyChanged(); } }

    private string _uiTabChat = "CHAT LOG";
    public string UiTabChat { get => _uiTabChat; set { _uiTabChat = value; OnPropertyChanged(); } }

    private string _uiTabSettings = "Cài đặt";
    public string UiTabSettings { get => _uiTabSettings; set { _uiTabSettings = value; OnPropertyChanged(); } }

    private string _uiCardVoiceHeader = "GIỌNG ĐỌC";
    public string UiCardVoiceHeader { get => _uiCardVoiceHeader; set { _uiCardVoiceHeader = value; OnPropertyChanged(); } }

    private string _uiCardVoiceSub = "  — Edge TTS tốc độ cao";
    public string UiCardVoiceSub { get => _uiCardVoiceSub; set { _uiCardVoiceSub = value; OnPropertyChanged(); } }

    private string _uiVoiceLabel = "Giọng nói:";
    public string UiVoiceLabel { get => _uiVoiceLabel; set { _uiVoiceLabel = value; OnPropertyChanged(); } }

    private string _uiSpeedLabel = "Tốc độ đọc:";
    public string UiSpeedLabel { get => _uiSpeedLabel; set { _uiSpeedLabel = value; OnPropertyChanged(); } }

    private string _uiOtherCustomLabel = "Tùy biến khác:";
    public string UiOtherCustomLabel { get => _uiOtherCustomLabel; set { _uiOtherCustomLabel = value; OnPropertyChanged(); } }

    private string _uiEdgeFreeNote = "⚡ Edge TTS miễn phí, mượt mà và tự nhiên nhất";
    public string UiEdgeFreeNote { get => _uiEdgeFreeNote; set { _uiEdgeFreeNote = value; OnPropertyChanged(); } }

    private string _uiCardCloneHeader = "CLONE GIỌNG & SLANG";
    public string UiCardCloneHeader { get => _uiCardCloneHeader; set { _uiCardCloneHeader = value; OnPropertyChanged(); } }

    private string _uiCloneLabel = "Clone:";
    public string UiCloneLabel { get => _uiCloneLabel; set { _uiCloneLabel = value; OnPropertyChanged(); } }

    private string _uiBrowseBtn = "File MP3";
    public string UiBrowseBtn { get => _uiBrowseBtn; set { _uiBrowseBtn = value; OnPropertyChanged(); } }

    private string _uiClearCloneBtn = "✕ Xóa";
    public string UiClearCloneBtn { get => _uiClearCloneBtn; set { _uiClearCloneBtn = value; OnPropertyChanged(); } }

    private string _uiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs 10k/tháng)";
    public string UiElevenLabsKeyLabel { get => _uiElevenLabsKeyLabel; set { _uiElevenLabsKeyLabel = value; OnPropertyChanged(); } }

    private string _uiSlangLabel = "Slang:";
    public string UiSlangLabel { get => _uiSlangLabel; set { _uiSlangLabel = value; OnPropertyChanged(); } }

    private string _uiCardPromptHeader = "PROMPT TRẢ LỜI AI (9Router Mac Mini)";
    public string UiCardPromptHeader { get => _uiCardPromptHeader; set { _uiCardPromptHeader = value; OnPropertyChanged(); } }

    private string _uiPromptDesc = " — Tùy biến phong cách và công thức đối đáp của AI";
    public string UiPromptDesc { get => _uiPromptDesc; set { _uiPromptDesc = value; OnPropertyChanged(); } }

    private string _uiResetPromptBtn = "↺ Khôi phục mặc định";
    public string UiResetPromptBtn { get => _uiResetPromptBtn; set { _uiResetPromptBtn = value; OnPropertyChanged(); } }

    private string _uiPromptHint = "💡 Gợi ý: Hướng dẫn AI cách xưng hô, tính cách, độ dài câu trả lời...";
    public string UiPromptHint { get => _uiPromptHint; set { _uiPromptHint = value; OnPropertyChanged(); } }

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

    public const string DefaultAiSystemPrompt = 
        "Bạn là trợ lý AI thông minh trong phòng livestream tiếng Việt.\n" +
        "Nhiệm vụ: Trả lời hoặc giao lưu ngắn gọn với khán giả cực kỳ tự nhiên, hóm hỉnh như người thật.\n" +
        "Quy tắc bắt buộc:\n" +
        "1. Trả lời CỰC KỲ NGẮN GỌN (chỉ đúng 1 câu, tối đa 15-20 từ).\n" +
        "2. Đi thẳng vào câu trả lời, TUYỆT ĐỐI KHÔNG mở đầu bằng 'Tôi là AI', 'AI nói', 'Chào bạn'.\n" +
        "3. Xưng hô tự nhiên (em/tui, gọi khán giả bằng tên hoặc bác).\n" +
        "4. Tuyệt đối không dùng emoji, icon, hay ký tự markdown (*, #, `) để giọng đọc phát âm mượt.\n" +
        "5. Khi gọi tên khán giả có chứa số (vd: Giang11241), chỉ gọi tên chữ cái ngắn gọn (vd: bác Giang) hoặc phát âm riêng lẻ từng số, không đọc thành hàng nghìn hay chục nghìn.";

    private string _aiSystemPrompt = DefaultAiSystemPrompt;

    public string AiSystemPrompt
    {
        get => _aiSystemPrompt;
        set
        {
            if (_aiSystemPrompt == value) return;
            _aiSystemPrompt = value;
            OnPropertyChanged();
        }
    }

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
    public ICommand ResetPromptCommand { get; }

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

        // Khởi tạo danh sách ngôn ngữ giao diện & giọng đọc
        foreach (var lang in LocalizationData.SupportedLanguages)
        {
            AvailableLanguages.Add(lang);
        }
        _selectedLanguage = AvailableLanguages[0]; // Mặc định Tiếng Việt
        LoadVoicesForLanguage("vi");

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
        ResetPromptCommand = new RelayCommand(() =>
        {
            var code = SelectedLanguage?.Code?.ToLower() ?? "vi";
            if (LocalizationData.DefaultPrompts.TryGetValue(code, out var def))
                AiSystemPrompt = def;
            else
                AiSystemPrompt = DefaultAiSystemPrompt;
        });

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

    public void LoadVoicesForLanguage(string langCode)
    {
        AvailableVoices.Clear();
        if (LocalizationData.VoicesByLanguage.TryGetValue(langCode, out var voices))
        {
            foreach (var v in voices) AvailableVoices.Add(v);
        }
        else if (LocalizationData.VoicesByLanguage.TryGetValue("vi", out var defaultVoices))
        {
            foreach (var v in defaultVoices) AvailableVoices.Add(v);
        }

        if (AvailableVoices.Count > 0)
        {
            SelectedVoiceId = AvailableVoices[0].Id;
        }
        _ttsOptions.Value.Speed = 1.3;
    }

    public void ApplyLanguage(LanguageInfo lang)
    {
        var code = lang.Code.ToLower();

        switch (code)
        {
            case "en":
                UiPlatformHeader = "LIVESTREAM SOURCES — Add YouTube, TikTok, Twitch...";
                UiPlatformLabel = "Platform:";
                UiChannelLabel = "Channel / ID:";
                UiUrlLabel = "URL / Link:";
                UiAddSource = "＋  Add Source";
                UiStart = "▶  START";
                UiStop = "⏹  STOP";
                UiAiReply = "AI Reply";
                UiClearLog = "🗑 Clear Log";
                UiCommentsCountSuffix = " comments";
                UiTabChat = "CHAT LOG";
                UiTabSettings = "Settings";
                UiCardVoiceHeader = "VOICE SETTINGS";
                UiCardVoiceSub = "  — High-speed Edge TTS";
                UiVoiceLabel = "Voice:";
                UiSpeedLabel = "Speed:";
                UiOtherCustomLabel = "Options:";
                UiEdgeFreeNote = "⚡ Edge TTS is free, smooth and natural";
                UiCardCloneHeader = "VOICE CLONE & SLANG";
                UiCloneLabel = "Clone:";
                UiBrowseBtn = "MP3 File";
                UiClearCloneBtn = "✕ Clear";
                UiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs)";
                UiSlangLabel = "Slang:";
                UiCardPromptHeader = "AI REPLY PROMPT (9Router Mac Mini)";
                UiPromptDesc = " — Customize AI response personality and rules";
                UiResetPromptBtn = "↺ Reset Default";
                UiPromptHint = "💡 Tip: Guide AI tone, humor, concise answers...";
                StatusText = IsRunning ? "🟢 Running..." : "🟢 Ready";
                break;

            case "zh":
                UiPlatformHeader = "直播源 — 添加 YouTube, TikTok, Twitch...";
                UiPlatformLabel = "平台:";
                UiChannelLabel = "频道 / ID:";
                UiUrlLabel = "URL / 链接:";
                UiAddSource = "＋  添加源";
                UiStart = "▶  开始";
                UiStop = "⏹  停止";
                UiAiReply = "AI 回复";
                UiClearLog = "🗑 清空日志";
                UiCommentsCountSuffix = " 条评论";
                UiTabChat = "弹幕日志";
                UiTabSettings = "设置";
                UiCardVoiceHeader = "语音设置";
                UiCardVoiceSub = "  — 高速 Edge TTS";
                UiVoiceLabel = "声音:";
                UiSpeedLabel = "语速:";
                UiOtherCustomLabel = "其他选项:";
                UiEdgeFreeNote = "⚡ Edge TTS 免费、流畅且自然";
                UiCardCloneHeader = "声音克隆与网络俚语";
                UiCloneLabel = "克隆:";
                UiBrowseBtn = "MP3 文件";
                UiClearCloneBtn = "✕ 删除";
                UiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs)";
                UiSlangLabel = "俚语:";
                UiCardPromptHeader = "AI 回复提示词 (9Router Mac Mini)";
                UiPromptDesc = " — 自定义AI对话风格与规则";
                UiResetPromptBtn = "↺ 恢复默认";
                UiPromptHint = "💡 提示：引导AI说话语气、幽默感、回答长度...";
                StatusText = IsRunning ? "🟢 运行中..." : "🟢 就绪";
                break;

            case "ja":
                UiPlatformHeader = "配信ソース — YouTube, TikTok, Twitchを追加...";
                UiPlatformLabel = "配信元:";
                UiChannelLabel = "チャンネル / ID:";
                UiUrlLabel = "URL / リンク:";
                UiAddSource = "＋  ソース追加";
                UiStart = "▶  開始";
                UiStop = "⏹  停止";
                UiAiReply = "AI 返信";
                UiClearLog = "🗑 ログ消去";
                UiCommentsCountSuffix = " コメント";
                UiTabChat = "チャットログ";
                UiTabSettings = "設定";
                UiCardVoiceHeader = "音声設定";
                UiCardVoiceSub = "  — 高速 Edge TTS";
                UiVoiceLabel = "音声:";
                UiSpeedLabel = "読み上げ速度:";
                UiOtherCustomLabel = "その他設定:";
                UiEdgeFreeNote = "⚡ Edge TTS は無料で滑らか、自然な発音です";
                UiCardCloneHeader = "ボイスクローン & スラング";
                UiCloneLabel = "クローン:";
                UiBrowseBtn = "MP3ファイル";
                UiClearCloneBtn = "✕ 削除";
                UiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs)";
                UiSlangLabel = "スラング:";
                UiCardPromptHeader = "AI 返信プロンプト (9Router Mac Mini)";
                UiPromptDesc = " — AI応答スタイルとルールのカスタマイズ";
                UiResetPromptBtn = "↺ デフォルトに戻す";
                UiPromptHint = "💡 ヒント：AIの口調、ユーモア、回答の長さなどを指示...";
                StatusText = IsRunning ? "🟢 実行中..." : "🟢 準備完了";
                break;

            case "ko":
                UiPlatformHeader = "라이브 소스 — YouTube, TikTok, Twitch 추가...";
                UiPlatformLabel = "플랫폼:";
                UiChannelLabel = "채널 / ID:";
                UiUrlLabel = "URL / 링크:";
                UiAddSource = "＋  소스 추가";
                UiStart = "▶  시작";
                UiStop = "⏹  중지";
                UiAiReply = "AI 응답";
                UiClearLog = "🗑 로그 삭제";
                UiCommentsCountSuffix = " 개 댓글";
                UiTabChat = "채팅 로그";
                UiTabSettings = "설정";
                UiCardVoiceHeader = "음성 설정";
                UiCardVoiceSub = "  — 고속 Edge TTS";
                UiVoiceLabel = "목소리:";
                UiSpeedLabel = "읽기 속도:";
                UiOtherCustomLabel = "기타 설정:";
                UiEdgeFreeNote = "⚡ Edge TTS는 무료이며 가장 매끄럽고 자연스럽습니다";
                UiCardCloneHeader = "음성 복제 및 슬랭";
                UiCloneLabel = "복제:";
                UiBrowseBtn = "MP3 파일";
                UiClearCloneBtn = "✕ 삭제";
                UiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs)";
                UiSlangLabel = "슬랭:";
                UiCardPromptHeader = "AI 응답 프롬프트 (9Router Mac Mini)";
                UiPromptDesc = " — AI 응답 스타일 및 규칙 사용자 지정";
                UiResetPromptBtn = "↺ 기본값 복원";
                UiPromptHint = "💡 팁: AI의 어투, 유머, 답변 길이 등을 지정...";
                StatusText = IsRunning ? "🟢 실행 중..." : "🟢 준비 완료";
                break;

            default:
                UiPlatformHeader = "NGUỒN LIVESTREAM — Thêm YouTube, TikTok, Twitch...";
                UiPlatformLabel = "Nền tảng:";
                UiChannelLabel = "Kênh / ID:";
                UiUrlLabel = "URL / Link:";
                UiAddSource = "＋  Thêm nguồn";
                UiStart = "▶  BẮT ĐẦU";
                UiStop = "⏹  DỪNG";
                UiAiReply = "Trả lời AI";
                UiClearLog = "🗑 Xóa log";
                UiCommentsCountSuffix = " bình luận";
                UiTabChat = "CHAT LOG";
                UiTabSettings = "Cài đặt";
                UiCardVoiceHeader = "GIỌNG ĐỌC";
                UiCardVoiceSub = "  — Edge TTS tốc độ cao";
                UiVoiceLabel = "Giọng nói:";
                UiSpeedLabel = "Tốc độ đọc:";
                UiOtherCustomLabel = "Tùy biến khác:";
                UiEdgeFreeNote = "⚡ Edge TTS miễn phí, mượt mà và tự nhiên nhất";
                UiCardCloneHeader = "CLONE GIỌNG & SLANG";
                UiCloneLabel = "Clone:";
                UiBrowseBtn = "File MP3";
                UiClearCloneBtn = "✕ Xóa";
                UiElevenLabsKeyLabel = "🔑 API Key (ElevenLabs 10k/tháng)";
                UiSlangLabel = "Slang:";
                UiCardPromptHeader = "PROMPT TRẢ LỜI AI (9Router Mac Mini)";
                UiPromptDesc = " — Tùy biến phong cách và công thức đối đáp của AI";
                UiResetPromptBtn = "↺ Khôi phục mặc định";
                UiPromptHint = "💡 Gợi ý: Hướng dẫn AI cách xưng hô, tính cách, độ dài câu trả lời...";
                StatusText = IsRunning ? "🟢 Đang chạy..." : "🟢 Sẵn sàng";
                break;
        }

        LoadVoicesForLanguage(code);

        if (LocalizationData.DefaultPrompts.TryGetValue(code, out var defaultPrompt))
        {
            AiSystemPrompt = defaultPrompt;
        }

        var langNotice = code switch
        {
            "en" => "🌐 Language switched to English. Native voice & AI prompt updated.",
            "zh" => "🌐 已切换为简体中文。本地语音与AI提示词已更新。",
            "ja" => "🌐 言語を日本語に切り替えました。ネイティブ音声とAIプロンプトを更新しました。",
            "ko" => "🌐 한국어로 변경되었습니다. 원어민 음성 및 AI 프롬프트가 업데이트되었습니다.",
            _ => "🌐 Đã chuyển sang Tiếng Việt. Giọng đọc & Prompt AI đã đồng bộ."
        };
        AddLogEntry("HỆ THỐNG", langNotice, "SYSTEM");
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

    public string FormatUsernameForSpeech(string? rawUsername)
    {
        var langCode = SelectedLanguage?.Code?.ToLower() ?? "vi";
        if (string.IsNullOrWhiteSpace(rawUsername))
        {
            return LocalizationData.DefaultViewerNames.TryGetValue(langCode, out var defName) 
                ? defName 
                : "Khán giả";
        }

        // Bỏ ký tự @ ở đầu nếu có
        var name = rawUsername.Trim().TrimStart('@');

        // Bỏ ký tự gạch dưới hoặc gạch nối ngăn cách để đọc mượt mà
        name = name.Replace("_", " ").Replace("-", " ");

        // Lấy bảng chữ số theo ngôn ngữ đang chọn
        if (!LocalizationData.DigitsByLanguage.TryGetValue(langCode, out var digitMap))
        {
            digitMap = LocalizationData.DigitsByLanguage["vi"];
        }

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (digitMap.TryGetValue(c, out var digitWord))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                    sb.Append(' ');

                sb.Append(digitWord);

                if (i + 1 < name.Length && char.IsLetter(name[i + 1]))
                    sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private string FormatSpeechText(string platform, string speechUsername, string content)
    {
        var langCode = SelectedLanguage?.Code?.ToLower() ?? "vi";
        var text = content ?? "";

        if (langCode == "vi")
        {
            text = Regex.Replace(text, @"(\d+)\s*\$", "$1 đô");
            text = text.Replace("$", " đô ");
            text = Regex.Replace(text, @"(\d+)\s*€", "$1 ơ-rô");
            text = text.Replace("€", " ơ-rô ");
            text = Regex.Replace(text, @"(\d+)\s*£", "$1 bảng");
            text = text.Replace("£", " bảng ");
            text = text.Replace("%", " phần trăm ");

            text = Regex.Replace(text, @"\bacc\b", "ạc", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bcmt\b", "comment", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bib\b", "inbox", RegexOptions.IgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(PrefixTemplate))
        {
            var prefix = PrefixTemplate.Replace("{Platform}", platform ?? "")
                                       .Replace("{Username}", speechUsername);
            if (!prefix.EndsWith(" ") && !prefix.EndsWith(":"))
                prefix += " ";
            return $"{prefix}{text}";
        }

        if (LocalizationData.SpeechFormats.TryGetValue(langCode, out var format))
        {
            return format.Replace("{Username}", speechUsername).Replace("{Content}", text);
        }

        return $"{speechUsername} nói là: {text}";
    }

    private async Task SpeakChatMessageAsync(string platform, string username, string content)
    {
        if (IsMuted) return;
        try
        {
            var normalizedContent = await NormalizeSlangAndTeencodeAsync(content);
            var speechUsername = FormatUsernameForSpeech(username);
            var langCode = SelectedLanguage?.Code?.ToLower() ?? "vi";

            if (langCode == "vi" && _ttsEngine is TLS1.AI.Engines.EdgeTtsEngine edgeTts && string.IsNullOrWhiteSpace(PrefixTemplate))
            {
                await edgeTts.SpeakCommentAsync(speechUsername, normalizedContent);
            }
            else
            {
                var textToSpeak = FormatSpeechText(platform, speechUsername, normalizedContent);
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
                        content = (SelectedLanguage?.Code?.ToLower() switch
                        {
                            "en" => "You are an English livestream chat normalizer. Task: Expand slang, chat acronyms, abbreviations, and crypto terms into natural clean English words for TTS speech synthesis. Strict rule: Return ONLY the normalized text, do not reply or add any greeting.",
                            "zh" => "你是直播间弹幕文本规范化助手。任务：将留言中的缩写、网络热梗、拼音缩写及加密货币术语转换为通顺自然的中文，以便TTS语音合成流畅发音。绝对规则：仅返回规范化后的纯文本，不要回答问题，不要添加任何寒暄或问候。",
                            "ja" => "あなたは配信チャットのテキスト正規化アシスタントです。タスク：コメント内の略語、ネットスラング、暗号資産用語をTTS音声合成が自然に読み上げられる日本語に変換します。絶対ルール：正規化されたテキストのみを返してください。質問への回答や挨拶は一切含めないでください。",
                            "ko" => "당신은 라이브 채팅 텍스트 정규화 어시스턴트입니다. 임무: 댓글 속의 줄임말, 신조어, 인터넷 유행어, 코인 용어를 TTS 음성이 매끄럽게 발음할 수 있는 자연스러운 표준어로 변환합니다. 절대 규칙: 정규화된 텍스트만 단독 반환하고 질문에 답하거나 인사를 추가하지 마세요.",
                            _ => "Bạn là bộ chuẩn hóa ngôn ngữ livestream tiếng Việt. Nhiệm vụ: Chuyển các từ viết tắt, teencode, tiếng lóng mạng xã hội và crypto trong câu sang tiếng Việt tự nhiên đầy đủ để giọng đọc AI phát âm chuẩn nhất. Quy tắc tối thượng: Chỉ trả về duy nhất câu đã chuẩn hóa, tuyệt đối không trả lời câu hỏi, không thêm bớt nội dung, không chào hỏi, không thêm icon."
                        })
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

            // 2. Nếu có bật Trả lời AI: Chờ AI trả lời câu này xong xuôi
            if (IsAiReplyEnabled && ShouldAiReply(username, content))
            {
                // Cho một nhịp nghỉ 1 giây ngắn tự nhiên giữa câu cmt và câu AI đáp
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!IsRunning || !IsAiReplyEnabled || IsMuted || token.IsCancellationRequested) return;

                // Xử lý và đọc câu trả lời AI (phát xong âm thanh câu này mới chạy tiếp)
                await ProcessAiReplyAsync(username, content);
            }

            // 3. Sau khi AI trả lời xong (hoặc đọc xong cmt nếu không bật AI):
            // Nghỉ 5 giây rồi MỚI ĐƯỢC giải phóng lock cho cmt tiếp theo được đọc!
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

        // Bỏ qua cmt chỉ toàn emoji hoặc ký tự đặc biệt (vd: :))) @@ ??? !!!)
        if (_spamPattern.IsMatch(trimmed)) return false;

        // Bỏ qua chuỗi cười hoặc spam ký tự vô nghĩa lặp lại (vd: hahaha, kkkk, hehehe)
        if (Regex.IsMatch(trimmed, @"^(ha|he|hi|ho|k|a|h){3,}$", RegexOptions.IgnoreCase)) return false;

        return true;
    }

    private async Task ProcessAiReplyAsync(string username, string userComment)
    {
        if (!IsAiReplyEnabled) return;
        var cts = _aiReplyCts;
        if (cts == null || cts.IsCancellationRequested) return;

        // Đảm bảo chỉ 1 câu trả lời AI được xử lý tại một thời điểm
        if (Interlocked.CompareExchange(ref _isProcessingAiReply, 1, 0) != 0) return;

        try
        {
            var speechUsername = FormatUsernameForSpeech(username);
            var reply = await RequestAiReplyFrom9RouterAsync(speechUsername, userComment, cts.Token);
            
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