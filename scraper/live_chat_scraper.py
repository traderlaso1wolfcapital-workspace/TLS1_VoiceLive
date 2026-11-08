"""
Live Chat Scraper - Playwright + BeautifulSoup
Chạy liên tục, không tự động dừng. Chỉ scrape bình luận MỚI.
"""

import asyncio, sys, json
from datetime import datetime
from playwright.async_api import async_playwright
from bs4 import BeautifulSoup

PLATFORM_SELECTORS = {
    "youtube": {
        "url_template": "https://www.youtube.com/live_chat?is_popout=1&v={channel}",
        "container": "yt-live-chat-text-message-renderer",
        "author": "#author-name",
        "message": "#message",
    },
    "twitch": {
        "url_template": "https://www.twitch.tv/popout/{channel}/chat",
        "container": "div.chat-line__message",
        "author": "span.chat-author__display-name",
        "message": "span.text-fragment",
    },
    "tiktok": {
        "url_template": "https://www.tiktok.com/@{channel}/live",
        "container": "div[data-e2e='chat-message']",
        "author": "span[data-e2e='chat-username']",
        "message": "span[data-e2e='chat-message-text']",
    },
    "generic": {
        "url_template": "{url}",
        "container": None, "author": None, "message": None,
    },
}

def now():
    return datetime.now().strftime("%H:%M:%S")

async def scrape(platform_id: str, url: str, channel: str):
    config = PLATFORM_SELECTORS.get(platform_id, PLATFORM_SELECTORS["generic"])
    if platform_id in ("youtube", "twitch", "tiktok", "facebook"):
        final_url = config["url_template"].format(channel=channel, url=url)
    else:
        final_url = url

    print(json.dumps({"status":"connecting","url":final_url,"time":now()}), flush=True)

    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True, args=[
            "--disable-blink-features=AutomationControlled",
            "--no-sandbox","--disable-web-security",
            "--disable-features=IsolateOrigins,site-per-process"
        ])
        context = await browser.new_context(
            viewport={"width":1280,"height":720},
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
        )
        page = await context.new_page()
        await page.add_init_script("""Object.defineProperty(navigator,'webdriver',{get:()=>undefined});window.chrome={runtime:{}};""")

        try:
            await page.goto(final_url, wait_until="domcontentloaded", timeout=30000)
            await page.wait_for_timeout(5000)
            print(json.dumps({"status":"connected","time":now()}), flush=True)
        except Exception as e:
            print(json.dumps({"status":"error","msg":str(e),"time":now()}), flush=True)
            await browser.close()
            return

        # Skip initial comments
        seen = set()
        html = await page.content()
        soup = BeautifulSoup(html, "html.parser")
        cs = config.get("container"); a_s = config.get("author"); m_s = config.get("message")
        for el in (soup.select(cs) if cs else soup.find_all(["div","span","li"], limit=200)):
            try:
                if a_s and m_s:
                    a = el.select_one(a_s); m = el.select_one(m_s)
                    if a and m: seen.add(f"{a.get_text(strip=True)}:{m.get_text(strip=True)}")
                else:
                    t = el.get_text(strip=True)
                    if 3 < len(t) < 500: seen.add(f"User:{t}")
            except: continue

        print(json.dumps({"status":"ready","skipped":len(seen),"msg":f"Bỏ qua {len(seen)} bình luận censored","time":now()}), flush=True)

        total = 0; heartbeat = 0

        # RUN FOREVER (C# side kills process when stopped)
        while True:
            try:
                html = await page.content()
                soup = BeautifulSoup(html, "html.parser")
                elements = soup.select(cs) if cs else soup.find_all(["div","span","li"], limit=200)

                found = 0
                for el in elements:
                    try:
                        author = "User"; message = ""
                        if a_s and m_s:
                            a_el = el.select_one(a_s); m_el = el.select_one(m_s)
                            if not a_el or not m_el: continue
                            author = a_el.get_text(strip=True); message = m_el.get_text(strip=True)
                        else:
                            text = el.get_text(strip=True)
                            if len(text) < 3 or len(text) > 500: continue
                            message = text
                        if not author or not message: continue

                        key = f"{author}:{message}"
                        if key in seen: continue
                        seen.add(key)

                        print(json.dumps({"type":"comment","platform":platform_id,"author":author,"message":message,"time":now()}), flush=True)
                        found += 1
                    except: continue

                if found > 0:
                    total += found
                    heartbeat = 0

                # Heartbeat every 60s to show still alive
                heartbeat += 1
                if heartbeat >= 30:
                    print(json.dumps({"status":"alive","total":total,"time":now()}), flush=True)
                    heartbeat = 0

                await page.wait_for_timeout(2000)

            except Exception as e:
                print(json.dumps({"status":"error","msg":str(e),"time":now()}), flush=True)
                await page.wait_for_timeout(5000)  # Retry after error

        await browser.close()

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"status":"error","msg":"Usage: python scraper.py <platform> <url_or_channel>"}))
        sys.exit(1)
    asyncio.run(scrape(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else ""))