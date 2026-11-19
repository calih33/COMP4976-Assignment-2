---
name: image-upload
description: How municipality logos and candidate photos are uploaded in Blazor Admin, validated, stored in SQL Server (StoredImage) and served by the API. Use when touching InputFile, StoredImage, LogoImageId/PhotoImageId, or image URLs.
---

# Image uploads

Admin and API are separate processes, so images are stored in SQL Server rather than on disk.

## Storage

`StoredImage`: `StoredImageId`, `ContentType`, `Data` (`varbinary(max)`), `MunicipalityId` (tenant-owned).
`Municipality.LogoImageId` and `Candidate.PhotoImageId` are nullable FKs. Images are optional.

## Restrictions (checked twice)

- Extensions `.jpg`, `.jpeg`, `.png`, `.webp`; content type must start with `image/`
- Max 2 MB
- Client: in the `InputFile` `OnChange` handler, before reading the stream
- Server: `ImageService.Validate(fileName, contentType, length)` in `MEMS.Data` (ported from A1 `CheckImage`)

## Blazor pattern

```razor
<InputFile accept=".jpg,.jpeg,.png,.webp" OnChange="OnPhotoSelected" />
<ValidationMessage For="() => _model.Photo" />

@code {
    private const long MaxBytes = 2 * 1024 * 1024;

    private async Task OnPhotoSelected(InputFileChangeEventArgs e)
    {
        var error = ImageService.Validate(e.File.Name, e.File.ContentType, e.File.Size);
        if (error is not null) { _photoError = error; return; }

        using var stream = e.File.OpenReadStream(MaxBytes); // throws above the limit
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        _model.Photo = new ImageUpload(e.File.ContentType, ms.ToArray());
    }
}
```

The service saves the `StoredImage` and the entity in the same `SaveChangesAsync`. On replace, the old
`StoredImage` row is removed in the same transaction.

## Serving

- Public/API: `GET /api/{tenant}/images/{id}` → `File(data, contentType)`, 404 when the image belongs to another tenant.
  DTOs expose `photoUrl` / `logoUrl` built from this route.
- Admin: an endpoint in `MEMS.Admin` (`/images/{id}`, `[Authorize]`) for previews.
- Fallback when null: `images/placeholder-candidate.svg` / `images/placeholder-logo.svg`.

## Verify

Upload a valid PNG (succeeds), a 3 MB JPG and a `.gif` (both rejected with a message). Replace and delete a record
and confirm the old `StoredImage` row is gone.
