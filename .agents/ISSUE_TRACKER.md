# NHẬT KÝ THEO DÕI LỖI & TIẾN ĐỘ DỰ ÁN TLS1_VOICELIVE

## 1. Lịch Sử Khắc Phục Lỗi & Tối Ưu
- **2026-11-08 (Antigravity):**
  - **Khảo sát file thực thi:** Xác định file `.exe` chạy chính thức là `TLS1 VoiceLive.exe` (121.6 MB, bản Self-Contained Single-File .NET 9).
  - **Dọn dẹp dung lượng:** Đã xóa bản sao lưu cũ `TLS1 VoiceLive - Copy.exe`, giải phóng 127 MB ổ cứng.
  - **Chuẩn hóa thư mục:** Chuyển đổi `publish2` thành `Release_App`, dọn dẹp thư mục thừa. Cấu trúc dự án tối giản và khoa học.
  - **Quy chuẩn công ty:** Đã thiết lập hoàn chỉnh `PLAN_TLS1_VOICELIVE.md` và `ISSUE_TRACKER.md` trong `.agents/`.
  - **Đồng bộ GitHub:** Đẩy toàn bộ mã nguồn lên repository `https://github.com/traderlaso1wolfcapital-workspace/TLS1_VoiceLive.git`.
  - **Sửa lỗi Scraper YouTube:** Cài đặt `playwright` + chromium/ffmpeg và `beautifulsoup4` cho Python 3.13 trên máy, khôi phục tính năng cào cmt live YouTube 100%.
  - **Sửa lỗi phát âm thanh (Silent Audio):** Bổ sung đầy đủ bộ thư viện `NAudio 2.3.0` vào build dependency, khắc phục lỗi mất tiếng khi đọc cmt.
  - **Tính năng mới:** Bổ sung tính năng **"Trả lời comment AI (9Router Mac Mini)"**:
    + Thêm ô tích checkbox trên giao diện WPF.
    + Kết nối API 9Router trên máy Mac Mini M1 (`http://192.168.100.154:20128/v1/chat/completions`).
    + Cập nhật API Key mới chuyên dụng: `sk-94f37e79b2d930fe-3qwzqr-6541de82` (Tên: `TLS1_VoiceLive`).
    + Tự động sinh câu trả lời ngắn gọn, tự nhiên và dùng TTS phát âm thanh đọc ngay sau khi đọc cmt của người xem.
    + Rebuild và cập nhật bản chạy hoàn chỉnh `Release_App\TLS1 VoiceLive.exe`.

## 2. Danh Sách Task Đang Chờ Xử Lý
- [x] Rà soát file `.exe` hoàn chỉnh cho Windows.
- [x] Tối ưu hóa & phân bổ lại cấu trúc thư mục dự án tối giản nhất.
- [x] Đẩy mã nguồn dự án lên GitHub `traderlaso1wolfcapital-workspace/TLS1_VoiceLive`.
- [x] Khắc phục lỗi cào cmt live YouTube do thiếu thư viện Python.
- [x] Khắc phục lỗi không phát ra âm thanh do thiếu driver `NAudio 2.3.0`.
- [x] Bổ sung Checkbox & tính năng "Trả lời comment AI" kết nối 9Router server Mac Mini M1.
- [x] Cập nhật API Key mới `sk-94f37e79b2d930fe-3qwzqr-6541de82` (TLS1_VoiceLive).
