namespace TLS1.VoiceLive.Models;

public class LanguageInfo
{
    public string Code { get; set; } = "vi";
    public string Name { get; set; } = "Tiếng Việt";
    public string Flag { get; set; } = "🇻🇳";
    public string DisplayText => $"{Flag}  {Name}";
    public override string ToString() => DisplayText;
}
