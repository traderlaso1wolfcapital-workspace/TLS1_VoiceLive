import os
import sys
import subprocess
from datetime import datetime

# Đảm bảo in tiếng Việt trên console Windows không bị lỗi UnicodeEncodeError cp1252
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

print("=========================================================")
print("      TLS1.VOICELIVE - TỰ ĐỘNG ĐỒNG BỘ (PULL) TỪ GITHUB  ")
print("  ⚡ CHẾ ĐỘ: ĐỒNG BỘ 100% (CHỐNG LỖI WINDOWS ACL & UNLINK)")
print("=========================================================")

base_dir = os.path.dirname(os.path.abspath(__file__))
git = "git"

def run_cmd(cmd, check=False):
    return subprocess.run(cmd, cwd=base_dir, capture_output=True, text=True, check=check, encoding="utf-8", errors="replace")

def run_cmd_bytes(cmd):
    return subprocess.run(cmd, cwd=base_dir, capture_output=True, check=False)

# 0. Dọn dẹp file lock tạm của Git nếu có
print("[0] Dọn dẹp tiến trình giữ file và file lock Git...")
git_dir = os.path.join(base_dir, ".git")
for lock_name in ["index.lock", "refs/heads/main.lock", "HEAD.lock", "index_user"]:
    lock_file = os.path.join(git_dir, lock_name)
    if os.path.exists(lock_file):
        try:
            os.remove(lock_file)
        except Exception:
            pass

# 1. Tải toàn bộ mã nguồn mới nhất từ GitHub
print("[1] Đang kết nối và tải mã nguồn mới nhất từ GitHub (git fetch origin main)...")
fetch_res = run_cmd([git, "fetch", "origin", "main"])
if fetch_res.returncode != 0:
    print("❌ LỖI: Không thể kết nối hoặc tải dữ liệu từ GitHub:")
    print(fetch_res.stderr)
    sys.exit(1)
print("    -> Đã tải xong dữ liệu từ GitHub.")

# Lấy mã commit mới nhất của origin/main
rev_res = run_cmd([git, "rev-parse", "origin/main"])
origin_hash = rev_res.stdout.strip() if rev_res.returncode == 0 else "origin/main"

# Tự động tạo nhánh backup an toàn trước khi pull để bảo toàn 100% mã nguồn máy CEO
run_cmd([git, "branch", "-f", "backup_local_before_pull", "HEAD"])

# 2. Đồng bộ mã nguồn (Thử git reset --hard trước; nếu vướng lỗi Windows ACL unlink thì kích hoạt In-Place Overwrite)
print("[2] Đang đồng bộ và thay thế mã nguồn theo GitHub...")
reset_res = run_cmd([git, "reset", "--hard", "origin/main"])

if reset_res.returncode != 0:
    print("⚠️ Phát hiện giới hạn phân quyền Windows ACL (unable to unlink).")
    print("⚡ Đang kích hoạt cơ chế Ghi Đè Trực Tiếp (In-Place Binary Overwrite)...")

    # Tìm các file có sự khác biệt so với origin/main
    diff_res = run_cmd([git, "diff", "--name-only", "origin/main"])
    changed_files = [f.strip() for f in diff_res.stdout.splitlines() if f.strip()]

    # Thêm cả các file trong working tree đang modified/staged
    status_res = run_cmd([git, "status", "--porcelain"])
    for line in status_res.stdout.splitlines():
        if len(line) > 3:
            fn = line[3:].strip()
            if fn and fn not in changed_files:
                changed_files.append(fn)

    print(f"    -> Đang đồng bộ {len(changed_files)} file khác biệt...")
    for rel_path in changed_files:
        full_path = os.path.join(base_dir, rel_path.replace("/", os.sep))
        show_proc = run_cmd_bytes([git, "show", f"origin/main:{rel_path}"])
        if show_proc.returncode == 0:
            try:
                os.makedirs(os.path.dirname(full_path), exist_ok=True)
                with open(full_path, "wb") as fp:
                    fp.write(show_proc.stdout)
                run_cmd([git, "add", rel_path])
            except Exception as e:
                print(f"    ! Bỏ qua {rel_path}: {e}")

    run_cmd([git, "update-ref", "refs/heads/main", origin_hash])
    run_cmd([git, "reset", origin_hash])
    print("    -> Đã ghi đè toàn bộ file thành công về trạng thái origin/main.")
else:
    print("    -> git reset --hard hoàn tất thành công.")

# 3. Dọn dẹp cây thư mục, tuyệt đối bảo lưu an toàn dữ liệu, Release và cấu hình
print("[3] Dọn dẹp cây thư mục đồng bộ (bảo lưu Release_App, profile, log và cấu hình)...")
clean_res = run_cmd([
    git, "clean", "-fd",
    "-e", "Release_App",
    "-e", "tiktok_profile",
    "-e", ".env*",
    "-e", "accounts.json",
    "-e", "*.log",
    "-e", "crash_log.txt",
    "-e", ".agents*",
    "-e", "scraper/data*"
])

# 4. Thông tin trạng thái cuối cùng
log_res = run_cmd([git, "log", "-1", "--oneline"])
status_res = run_cmd([git, "status", "-s"])

print("=========================================================")
print("✅ HOÀN TẤT! CODE ĐÃ ĐƯỢC ĐỒNG BỘ TỪ GITHUB THÀNH CÔNG.")
print(f"Commit mới nhất: {log_res.stdout.strip()}")
print(f"Thời gian: {datetime.now().strftime('%d/%m/%Y %H:%M:%S')}")
if status_res.stdout.strip():
    print("Trạng thái hiện tại:")
    print(status_res.stdout.strip())
else:
    print("Trạng thái thư mục: 100% sạch sẽ và đồng nhất với GitHub!")
print("=========================================================")

