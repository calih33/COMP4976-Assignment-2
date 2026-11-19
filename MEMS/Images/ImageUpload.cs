namespace MEMS.Images;

/// <summary>An image the user picked, already read into memory (max 2 MB).</summary>
public sealed record ImageUpload(string FileName, string ContentType, byte[] Data);
