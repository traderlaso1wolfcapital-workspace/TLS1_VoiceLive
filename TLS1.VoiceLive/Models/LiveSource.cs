using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TLS1.VoiceLive.Models;

/// <summary>
/// Một nguồn livestream người dùng thêm vào để lấy bình luận.
/// Hỗ trợ nhiều nguồn cùng lúc.
/// </summary>
public class LiveSource : INotifyPropertyChanged
{
    private string _platformId = "demo";
    private string _platformName = "Demo";
    private string _channelName = "";
    private string _url = "";
    private string _error = "";

    public string PlatformId { get => _platformId; set { _platformId = value; OnPropertyChanged(); } }
    public string PlatformName { get => _platformName; set { _platformName = value; OnPropertyChanged(); } }
    public string ChannelName { get => _channelName; set { _channelName = value; OnPropertyChanged(); } }
    public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }

    /// <summary>
    /// Thông báo lỗi của nguồn này (rỗng nếu không có lỗi).
    /// Hiển thị vĩnh viễn đến khi xóa nguồn.
    /// </summary>
    public string Error
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public string DisplayText => string.IsNullOrEmpty(_error)
        ? $"[{PlatformName}] {(string.IsNullOrEmpty(ChannelName) ? Url : ChannelName)}"
        : $"[{PlatformName}] ❌ {_error}";

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}