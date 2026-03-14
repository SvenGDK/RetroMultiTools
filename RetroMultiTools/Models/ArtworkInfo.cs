namespace RetroMultiTools.Models;

public class ArtworkInfo
{
    public byte[]? BoxArt { get; set; }
    public byte[]? Snap { get; set; }
    public byte[]? TitleScreen { get; set; }

    public bool HasAnyArtwork => BoxArt != null || Snap != null || TitleScreen != null;
}
