# BẢN VẼ KIẾN TRÚC DỰ ÁN TLS1_VOICELIVE

## 1. Mục Tiêu Dự Án
- Ứng dụng Desktop (WPF .NET 9) tích hợp AI Voice (Edge-TTS) và cào bình luận đa nền tảng (TikTok, YouTube, Twitch, Facebook Live) theo thời gian thực.
- Đọc to comment của khán giả bằng giọng nói AI tự nhiên tiếng Việt, hỗ trợ livestream bán hàng / tương tác crypto.

## 2. Kiến Trúc Hệ Thống
- **Frontend / Desktop UI:** WPF .NET 9 (`TLS1.VoiceLive`) theo mô hình MVVM.
- **Core Engine & Aggregator:**
  - `TLS1.Chat`: Provider cào comment đa nền tảng (kết nối Python scraper qua WebSocket/Console Bridge).
  - `TLS1.AI`: Engine Edge-TTS sinh giọng nói AI thời gian thực.
  - `TLS1.Core` & `TLS1.Infrastructure`: Cấu hình, DI, Logging Serilog.
- **Scraper Service (Python):**
  - `live_chat_scraper.py`, `tiktok_live_scraper.py`, `viet_word_segment.py`
  - Kết nối TikTok Live via session profile Chromium.

## 3. Cấu Trúc Thư Mục Chuẩn
- `.agents/`: Tài liệu kiến trúc và theo dõi lỗi.
- `Release_App/`: Bản build Portable hoàn chỉnh cho Windows (.NET 9 Self-Contained Single File).
- `TLS1.VoiceLive/`: Mã nguồn dự án WPF C#.
- `scraper/`: Mã nguồn Python phục vụ phát triển & test scraper.
- `TLS1.VoiceLive.sln`: Solution Visual Studio.

