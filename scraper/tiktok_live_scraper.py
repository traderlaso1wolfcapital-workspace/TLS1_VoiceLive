"""
TikTok LIVE scraper - Dùng Chrome thật cài trên máy (không phải Chromium test).
Lần đầu: mở Chrome thật để bạn đăng nhập → session lưu tự động.
Lần sau: dùng lại session đã lưu.
"""

import asyncio, sys, json, os
from datetime import datetime
from playwright.async_api import async_playwright

PROFILE_DIR = os.path.join(os.path.dirname(__file__), "..", "tiktok_profile")

def now():
    return datetime.now().strftime("%H:%M:%S")

async def scrape_tiktok(channel: str, url: str = ""):
    if not url and channel:
        url = f"https://www.tiktok.com/@{channel}/live"
    elif not url:
        print(json.dumps({"status":"error","msg":"Can username hoac URL","time":now()}), flush=True)
        return

    print(json.dumps({"status":"connecting","url":url,"time":now()}), flush=True)

    async with async_playwright() as p:
        os.makedirs(PROFILE_DIR, exist_ok=True)
        
        # Dùng Chrome thật cài trên máy (channel="chrome") thay vì Chromium test
        try:
            context = await p.chromium.launch_persistent_context(
                user_data_dir=PROFILE_DIR,
                channel="chrome",  # <<< DÙNG CHROME THẬT
                headless=False,
                viewport={"width": 1280, "height": 720},
                args=[
                    "--disable-blink-features=AutomationControlled",
                    "--disable-infobars",
                ]
            )
        except Exception:
            # Fallback: nếu không tìm thấy Chrome → dùng Chromium
            print(json.dumps({"status":"warning","msg":"Khong tim thay Chrome cai dat, dung Chromium...","time":now()}), flush=True)
            context = await p.chromium.launch_persistent_context(
                user_data_dir=PROFILE_DIR,
                headless=False,
                viewport={"width": 1280, "height": 720},
                args=["--disable-blink-features=AutomationControlled", "--disable-infobars"]
            )

        page = context.pages[0] if context.pages else await context.new_page()
        await page.add_init_script("""Object.defineProperty(navigator,'webdriver',{get:()=>undefined});window.chrome={runtime:{}};""")

        try:
            await page.goto(url, wait_until="domcontentloaded", timeout=60000)
            await page.wait_for_timeout(10000)

            need_login = await page.evaluate("""() => {
                return document.querySelector('[data-e2e="login-button"], .login-button, [class*="login-modal"]')
                    || document.body.innerText.includes('Log in to continue');
            }""")

            if need_login:
                print(json.dumps({"status":"need_login","msg":"Vui long dang nhap TikTok. Session se duoc luu.","time":now()}), flush=True)
                for i in range(120):
                    await page.wait_for_timeout(5000)
                    still = await page.evaluate("""() => {
                        return document.querySelector('[data-e2e="login-button"], .login-button') !== null;
                    }""")
                    if not still:
                        await page.goto(url, wait_until="domcontentloaded", timeout=30000)
                        await page.wait_for_timeout(5000)
                        print(json.dumps({"status":"logged_in","msg":"Da dang nhap!","time":now()}), flush=True)
                        break
                else:
                    print(json.dumps({"status":"error","msg":"Het thoi gian dang nhap.","time":now()}), flush=True)
                    await context.close()
                    return

            print(json.dumps({"status":"connected","time":now()}), flush=True)
        except Exception as e:
            print(json.dumps({"status":"error","msg":str(e),"time":now()}), flush=True)
            await context.close()
            return

        # Inject JS scraper
        await page.evaluate("""
            if (!window.__tt) {
                window.__tt = true; window.__s = new Set();
                setInterval(() => {
                    document.querySelectorAll('[class*="DivChatMessage"], [class*="chat-message"], [class*="ChatMessage"], [data-e2e*="chat"]').forEach(r => {
                        try {
                            const n = r.querySelector('[class*="nickname"], [class*="username"], [class*="UserName"]');
                            const t = r.querySelector('[class*="comment"], [class*="text"], [class*="Content"], span');
                            if (n && t) {
                                const u = n.innerText.replace(/[:：]/g,'').trim();
                                const m = t.innerText.trim();
                                if (!u || !m || u.length>50 || m.length>500) return;
                                const k = u+':'+m;
                                if (!window.__s.has(k)) { window.__s.add(k); console.log('__TT__'+JSON.stringify({author:u,message:m})); }
                            }
                        } catch(e) {}
                    });
                }, 1000);
            }
        """)

        seen = set()
        def on_console(msg):
            t = msg.text
            if t.startswith("__TT__"):
                try:
                    d = json.loads(t[6:])
                    k = f"{d['author']}:{d['message']}"
                    if k not in seen:
                        seen.add(k)
                        print(json.dumps({"type":"comment","platform":"tiktok","author":d["author"],"message":d["message"],"time":now()}), flush=True)
                except: pass
        page.on("console", on_console)

        print(json.dumps({"status":"ready","msg":"Bat dau scrape","time":now()}), flush=True)

        hb = 0
        while True:
            await page.wait_for_timeout(5000)
            hb += 1
            if hb >= 12:
                print(json.dumps({"status":"alive","total":len(seen),"time":now()}), flush=True)
                hb = 0

if __name__ == "__main__":
    channel = sys.argv[1] if len(sys.argv) > 1 else ""
    url = sys.argv[2] if len(sys.argv) > 2 else ""
    asyncio.run(scrape_tiktok(channel, url))