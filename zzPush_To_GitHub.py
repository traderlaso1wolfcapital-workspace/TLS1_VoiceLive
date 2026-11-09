import json
import re
import os
import sys
import subprocess
from datetime import datetime

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ⚠️ CỜ TỰ ĐỘNG TĂNG PHIÊN BẢN (VERSION)
# Khi True: Mỗi lần push có thay đổi sẽ tự động tăng số Patch (vd: 2.2.0 -> 2.2.1) và đồng bộ vào MainWindow.xaml
AUTO_BUMP_VERSION = True

def bump_version(v: str) -> str:
    parts = v.split('.')
    while len(parts) < 3:
        parts.append('0')
    parts[-1] = str(int(parts[-1]) + 1)
    return '.'.join(parts)

def update_xaml_version(base_dir: str, new_v: str):
    xaml_path = os.path.join(base_dir, "TLS1.VoiceLive", "MainWindow.xaml")
    if not os.path.exists(xaml_path):
        return
    try:
        with open(xaml_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Cập nhật Text=" vX.X" trong Titlebar
        # VD: <TextBlock Text=" v2.2" FontSize="11" ...
        short_v = '.'.join(new_v.split('.')[:2]) if len(new_v.split('.')) >= 2 else new_v
        pattern = r'(<TextBlock\s+Text=")\s*v[0-9.]+(\s*"\s+FontSize="11")'
        if re.search(pattern, content):
            content = re.sub(pattern, rf'\1 v{short_v}\2', content, count=1)
            with open(xaml_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"[1.1] Đã đồng bộ Version v{short_v} vào giao diện MainWindow.xaml")
    except Exception as e:
        print(f"[1.1] Lỗi khi cập nhật XAML: {e}")

def push_to_github() -> bool:
    print("=========================================================")
    print("       TLS1.VOICELIVE - TỰ ĐỘNG ĐẨY CODE LÊN GITHUB      ")
    print("=========================================================")

    base_dir = os.path.dirname(os.path.abspath(__file__))
    v_path = os.path.join(base_dir, "version.json")
    git = "git"

    # 0. Dọn dẹp các file lock tạm của Git nếu có
    git_dir = os.path.join(base_dir, ".git")
    for lock_name in ["index.lock", "refs/heads/main.lock", "HEAD.lock", "index_user"]:
        lock_file = os.path.join(git_dir, lock_name)
        if os.path.exists(lock_file):
            try:
                os.remove(lock_file)
            except Exception:
                pass

    # 1. Thu thập và chuẩn bị lưu thay đổi (Commit)
    print("[1] Đang kiểm tra các thay đổi mã nguồn...")
    subprocess.run([git, "add", "."], check=False, cwd=base_dir)

    # 1.1 Bảo vệ an toàn: Loại bỏ các thư mục build / nhị phân nặng / token / file cá nhân
    sensitive_excludes = [
        "Release_App", "publish", "publish2", "bin", "obj", "build_bin", "build_obj",
        "tiktok_profile", "crash_log.txt", ".env", "accounts.json", "temp_patch.py"
    ]
    existing_excludes = [f for f in sensitive_excludes if os.path.exists(os.path.join(base_dir, f))]
    if existing_excludes:
        print(f"[1.1] Bảo vệ tài sản & loại bỏ các file ngoại lệ / binary nặng (>100MB):")
        for exc in existing_excludes:
            print(f"      - Loại trừ: {exc}")
        subprocess.run([git, "reset", "HEAD"] + existing_excludes, check=False, cwd=base_dir)

    # Kiểm tra xem có thay đổi nào cần commit không
    status_proc = subprocess.run([git, "status", "--porcelain"], capture_output=True, text=True, check=False, cwd=base_dir, encoding="utf-8", errors="replace")
    has_changes = bool(status_proc.stdout.strip())

    # 2. Quản lý Version & Commit
    new_v = "2.2.0"
    if os.path.exists(v_path):
        try:
            with open(v_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            old_v = data.get('version', "2.2.0")
            if has_changes and AUTO_BUMP_VERSION:
                new_v = bump_version(old_v)
                data['version'] = new_v
                with open(v_path, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=4)
                print(f"[2] Đã tự động tăng Version từ v{old_v} -> v{new_v}")
                update_xaml_version(base_dir, new_v)
                subprocess.run([git, "add", "version.json"], check=False, cwd=base_dir)
                xaml_p = os.path.join("TLS1.VoiceLive", "MainWindow.xaml")
                if os.path.exists(os.path.join(base_dir, xaml_p)):
                    subprocess.run([git, "add", xaml_p], check=False, cwd=base_dir)
            else:
                new_v = old_v
                print(f"[2] Giữ nguyên Version: v{new_v}")
        except Exception as e:
            print(f"[2] Bỏ qua cập nhật version.json: {e}")

    if has_changes:
        commit_msg = f"Update TLS1 VoiceLive v{new_v}: Stream & AI Sync"
        if len(sys.argv) > 1 and sys.argv[1].strip():
            commit_msg = f"Update TLS1 VoiceLive v{new_v}: {sys.argv[1].strip()}"

        print(f"[3] Đang tạo Commit: '{commit_msg}'...")
        subprocess.run([
            git,
            "-c", "user.name=TLS1 Admin",
            "-c", "user.email=admin@tls1.com",
            "commit",
            "-m", commit_msg
        ], check=False, cwd=base_dir)
    else:
        print("[3] Working tree sạch sẽ, không có file nào thay đổi mới.")

    # 4. Đồng bộ dữ liệu mới nhất từ GitHub trước khi đẩy (tránh conflict)
    print("[4] Đang kiểm tra và đồng bộ từ GitHub (Fetch & Rebase)...")
    fetch_res = subprocess.run([git, "fetch", "origin", "main"], check=False, cwd=base_dir)
    if fetch_res.returncode == 0:
        subprocess.run([git, "pull", "--rebase", "origin", "main"], check=False, cwd=base_dir)

    # 5. Đẩy lên GitHub
    print("[5] Đang đẩy code lên GitHub repository...")
    push_res = subprocess.run([git, "push", "origin", "main"], check=False, cwd=base_dir)

    # Lấy thông tin commit mới nhất để hiển thị trực quan cho CEO
    log_proc = subprocess.run([git, "log", "-1", "--oneline"], capture_output=True, text=True, check=False, cwd=base_dir, encoding="utf-8", errors="replace")
    latest_commit = log_proc.stdout.strip() if log_proc.returncode == 0 else ""

    print("=========================================================")
    success = (push_res.returncode == 0)
    if success:
        print("✅ HOÀN TẤT! CODE ĐÃ ĐƯỢC ĐẨY LÊN GITHUB THÀNH CÔNG.")
        if latest_commit:
            print(f"Commit mới nhất: {latest_commit}")
        print(f"Phiên bản: v{new_v}")
        print(f"Thời gian: {datetime.now().strftime('%d/%m/%Y %H:%M:%S')}")
    else:
        print("⚠️ Có lỗi khi đẩy code lên GitHub (kiểm tra lại kết nối mạng hoặc quyền truy cập).")
    print("=========================================================")
    return success

if __name__ == "__main__":
    success = push_to_github()
    sys.exit(0 if success else 1)
