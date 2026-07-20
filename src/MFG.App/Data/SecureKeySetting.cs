namespace MFG.App.Data;

public sealed class SecureKeySetting
{
    public int Id { get; set; }
    public string Sha256 { get; set; } = null!;
    public DateTimeOffset ImportedAt { get; set; }
}
