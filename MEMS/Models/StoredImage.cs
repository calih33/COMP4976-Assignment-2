namespace MEMS.Models;

/// <summary>
/// An uploaded logo or candidate photo stored in SQL Server. Admin and the API are separate processes,
/// so images live in the database instead of a local uploads folder.
/// Tenant ownership comes from the municipality/candidate that points at it.
/// </summary>
public class StoredImage
{
    public int StoredImageId { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];

    public DateTime DateUploaded { get; set; } = DateTime.UtcNow;
}
