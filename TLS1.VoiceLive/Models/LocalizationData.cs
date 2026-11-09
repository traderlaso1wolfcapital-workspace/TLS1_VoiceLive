using System.Collections.Generic;

namespace TLS1.VoiceLive.Models;

public static class LocalizationData
{
    public static readonly List<LanguageInfo> SupportedLanguages = new()
    {
        new() { Code = "vi", Name = "Tiếng Việt", Flag = "🇻🇳" },
        new() { Code = "en", Name = "English", Flag = "🇬🇧" },
        new() { Code = "zh", Name = "简体中文", Flag = "🇨🇳" },
        new() { Code = "ja", Name = "日本語", Flag = "🇯🇵" },
        new() { Code = "ko", Name = "한국어", Flag = "🇰🇷" }
    };

    public static readonly Dictionary<string, List<VoiceInfo>> VoicesByLanguage = new()
    {
        ["vi"] = new()
        {
            new() { Id = "vi-VN-HoaiMyNeural", Name = "Hoài My (Nữ - Tiếng Việt)", Gender = "Female", Description = "Giọng nữ truyền cảm" },
            new() { Id = "vi-VN-NamMinhNeural", Name = "Nam Minh (Nam - Tiếng Việt)", Gender = "Male", Description = "Giọng nam ấm áp" }
        },
        ["en"] = new()
        {
            new() { Id = "en-US-JennyNeural", Name = "Jenny (Female - English US)", Gender = "Female", Description = "Warm, natural US English" },
            new() { Id = "en-US-GuyNeural", Name = "Guy (Male - English US)", Gender = "Male", Description = "Confident, friendly US English" },
            new() { Id = "en-US-AriaNeural", Name = "Aria (Female - English US)", Gender = "Female", Description = "Expressive, cheerful US English" },
            new() { Id = "en-GB-SoniaNeural", Name = "Sonia (Female - English UK)", Gender = "Female", Description = "Polite, crisp UK English" }
        },
        ["zh"] = new()
        {
            new() { Id = "zh-CN-XiaoxiaoNeural", Name = "晓晓 (女 - 简体中文)", Gender = "Female", Description = "温暖、亲切女声" },
            new() { Id = "zh-CN-YunxiNeural", Name = "云希 (男 - 简体中文)", Gender = "Male", Description = "沉稳、自然男声" },
            new() { Id = "zh-CN-YunjianNeural", Name = "云健 (男 - 影视风)", Gender = "Male", Description = "激情生动男声" }
        },
        ["ja"] = new()
        {
            new() { Id = "ja-JP-NanamiNeural", Name = "七海 Nanami (女性 - 日本語)", Gender = "Female", Description = "親しみやすく明るい女性の声" },
            new() { Id = "ja-JP-KeitaNeural", Name = "圭太 Keita (男性 - 日本語)", Gender = "Male", Description = "聞き取りやすい爽やかな男性の声" }
        },
        ["ko"] = new()
        {
            new() { Id = "ko-KR-SunHiNeural", Name = "선희 Sun-Hi (여성 - 한국어)", Gender = "Female", Description = "밝고 자연스러운 여성 목소리" },
            new() { Id = "ko-KR-InJoonNeural", Name = "인준 InJoon (남성 - 한국어)", Gender = "Male", Description = "부드럽고 안정감 있는 남성 목소리" }
        }
    };

    public static readonly Dictionary<string, string> DefaultPrompts = new()
    {
        ["vi"] = 
            "Bạn là một trợ lý livestream thông minh, hài hước và thân mật của streamer. Nhiệm vụ của bạn là trả lời các bình luận của khán giả một cách tự nhiên, ngắn gọn (1-2 câu ngắn, dưới 30 từ), dí dỏm và cuốn hút.\n" +
            "1. Đi thẳng vào câu trả lời, TUYỆT ĐỐI KHÔNG mở đầu bằng 'Tôi là AI', 'AI nói', 'Chào bạn'.\n" +
            "2. Xưng hô tự nhiên (em/tui, gọi khán giả bằng tên hoặc bác).\n" +
            "3. Tuyệt đối không dùng emoji, icon, hay ký tự markdown (*, #, `) để giọng đọc phát âm mượt.\n" +
            "4. Khi gọi tên khán giả có chứa số (vd: Giang11241), chỉ gọi tên chữ cái ngắn gọn (vd: bác Giang) hoặc phát âm riêng lẻ từng số, không đọc thành hàng nghìn hay chục nghìn.",

        ["en"] = 
            "You are a smart, witty, and friendly livestream AI co-host for the streamer. Your job is to reply to audience chat comments naturally, concisely (1-2 short sentences, under 30 words), engaging and funny.\n" +
            "1. Answer directly! NEVER start with 'I am an AI', 'AI says', or 'Hello'.\n" +
            "2. Address the audience naturally (buddy, bro, or by their name).\n" +
            "3. Absolutely NO emojis, icons, or markdown symbols (*, #, `) so TTS speech reads seamlessly.\n" +
            "4. When calling a username that has numbers (e.g. Giang11241), only call the name letters or pronounce each digit individually, never say thousands.",

        ["zh"] = 
            "你是一位机智、幽默且亲切的直播间智能副播。你的任务是用简短自然（1-2句短句，30字以内）、生动风趣的语言回复观众的弹幕留言。\n" +
            "1. 直接切入回答，绝对不要用'我是AI'、'AI回复'或'你好'等套话开头。\n" +
            "2. 称呼亲切自然（老哥、朋友、或观众名字）。\n" +
            "3. 严禁使用表情符号（emoji）、特殊符号或Markdown排版（*, #, `），以保证语音播报纯净流畅。\n" +
            "4. 遇到带有数字的用户名时，只读字母名字或单独念出每个数字，切勿读成成千上万的大数。",

        ["ja"] = 
            "あなたは配信者のための機知に富んだフレンドリーなライブ配信アシスタントです。視聴者のコメントに対して、自然で簡潔（1〜2文、30文字程度）、親しみやすく面白いトーンで返答してください。\n" +
            "1. 挨拶や「私はAIです」といった前置きは一切せず、直接回答に入ってください。\n" +
            "2. リスナーに対して自然に呼びかけてください（さん付けや親しみを込めた呼び方）。\n" +
            "3. 音声読み上げをスムーズにするため、絵文字、記号、Markdown（*, #, `）は使用しないでください。\n" +
            "4. 数字が含まれる名前は、名前部分のみ呼ぶか、数字を1桁ずつ個別に読んでください。",

        ["ko"] = 
            "당신은 센스 있고 유쾌하며 친근한 라이브 방송 AI 어시스턴트입니다. 시청자의 댓글에 대해 자연스럽고 간결하게(1~2문장, 30자 이내), 재치 있게 답변하세요.\n" +
            "1. '저는 AI입니다', '안녕하세요' 같은 서두 없이 즉시 본론으로 답변하세요.\n" +
            "2. 시청자를 친근하게 부르세요(님 또는 이름).\n" +
            "3. 음성 합성(TTS)이 매끄럽게 발음할 수 있도록 이모지, 특수문자, 마크다운(*, #, `)은 절대 사용하지 마세요.\n" +
            "4. 닉네임에 숫자가 있는 경우 이름만 부르거나 각 숫자를 낱개로 발음하세요."
    };

    public static readonly Dictionary<string, Dictionary<char, string>> DigitsByLanguage = new()
    {
        ["vi"] = new()
        {
            ['0'] = "không", ['1'] = "một", ['2'] = "hai", ['3'] = "ba", ['4'] = "bốn",
            ['5'] = "năm", ['6'] = "sáu", ['7'] = "bảy", ['8'] = "tám", ['9'] = "chín"
        },
        ["en"] = new()
        {
            ['0'] = "zero", ['1'] = "one", ['2'] = "two", ['3'] = "three", ['4'] = "four",
            ['5'] = "five", ['6'] = "six", ['7'] = "seven", ['8'] = "eight", ['9'] = "nine"
        },
        ["zh"] = new()
        {
            ['0'] = "零", ['1'] = "一", ['2'] = "二", ['3'] = "三", ['4'] = "四",
            ['5'] = "五", ['6'] = "六", ['7'] = "七", ['8'] = "八", ['9'] = "九"
        },
        ["ja"] = new()
        {
            ['0'] = "ゼロ", ['1'] = "いち", ['2'] = "に", ['3'] = "さん", ['4'] = "よん",
            ['5'] = "ご", ['6'] = "ろく", ['7'] = "なな", ['8'] = "はち", ['9'] = "きゅう"
        },
        ["ko"] = new()
        {
            ['0'] = "영", ['1'] = "일", ['2'] = "이", ['3'] = "삼", ['4'] = "사",
            ['5'] = "오", ['6'] = "육", ['7'] = "칠", ['8'] = "팔", ['9'] = "구"
        }
    };

    public static readonly Dictionary<string, string> DefaultViewerNames = new()
    {
        ["vi"] = "Khán giả",
        ["en"] = "Viewer",
        ["zh"] = "观众",
        ["ja"] = "視聴者",
        ["ko"] = "시청자"
    };

    public static readonly Dictionary<string, string> SpeechFormats = new()
    {
        ["vi"] = "{Username} nói là: {Content}",
        ["en"] = "{Username} says: {Content}",
        ["zh"] = "{Username} 说：{Content}",
        ["ja"] = "{Username} さん：「{Content}」",
        ["ko"] = "{Username} 님의 말: {Content}"
    };
}
