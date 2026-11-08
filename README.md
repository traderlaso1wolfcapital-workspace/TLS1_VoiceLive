# TLS1_VoiceLive

Hệ thống Desktop Live Stream Voice AI đa nền tảng (TikTok, YouTube, Twitch, Facebook Live).

## 🚀 Tính Năng Chính
- **AI Text-To-Speech (TTS):** Chuyển đổi bình luận khán giả thành giọng nói tiếng Việt tự nhiên thời gian thực (hỗ trợ Edge-TTS, Google Cloud TTS).
- **Multi-Platform Live Chat Scraper:** Tự động kết nối và thu thập bình luận trực tiếp từ TikTok LIVE, YouTube, Twitch.
- **WPF .NET 9 UI:** Giao diện trực quan, quản lý danh sách nguồn phát, nhật ký chat, điều chỉnh âm lượng, tốc độ và lựa chọn giọng đọc.

## 🛠️ Công Nghệ Sử Dụng
- **Frontend / UI:** C# WPF (.NET 9.0)
- **Live Scraper Engine:** Python (Playwright Chromium persistent context)
- **Voice Synthesis:** Edge-TTS / Google Cloud Text-to-Speech

## 📁 Cấu Trúc Dự Án
```text
TLS1_VoiceLive/
├── .agents/          # Kiến trúc hệ thống và theo dõi tiến độ công ty
├── TLS1.VoiceLive/   # Mã nguồn C# WPF (.NET 9)
│   └── lib/          # Thư viện core liên kết (TLS1.Core, TLS1.AI, TLS1.Chat, ...)
├── scraper/          # Python scripts phục vụ cào bình luận
├── TLS1.VoiceLive.sln# Visual Studio Solution
└── .gitignore
```

