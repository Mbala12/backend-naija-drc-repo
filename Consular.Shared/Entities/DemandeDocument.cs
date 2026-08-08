namespace Consular.Shared.Entities;

public class DemandeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DemandeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string CheminStockage { get; set; } = string.Empty; // e.g. S3/Blob URL — not raw bytes in Postgres
    public string DocumentKind { get; set; } = string.Empty; // "passport_scan", "birth_certificate", etc.
    public bool ValideParAgent { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
