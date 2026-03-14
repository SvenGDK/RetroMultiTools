namespace RetroMultiTools.Models;

public class RomInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public RomSystem System { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public Dictionary<string, string> HeaderInfo { get; set; } = [];
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
