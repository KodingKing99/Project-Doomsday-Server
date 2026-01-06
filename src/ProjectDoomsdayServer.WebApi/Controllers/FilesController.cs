using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Files;
using Microsoft.AspNetCore.Mvc;

namespace ProjectDoomsdayServer.WebApi.Controllers;

[ApiController]
[Route("files")]
public sealed class FilesController : ControllerBase
{
    private readonly FileService _filesService;
    public FilesController(FileService fileService) => _filesService = fileService;

    [HttpPost]
    [RequestSizeLimit(524_288_000)]
    public async Task<ActionResult<FileRecord>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        await using var stream = file.OpenReadStream();
        var rec = await _filesService.UploadAsync(file.FileName, file.ContentType ?? "application/octet-stream", stream, ct);
        return CreatedAtAction(nameof(GetById), new { id = rec.Id }, rec);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileRecord>>> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _filesService.ListAsync(skip, Math.Clamp(take, 1, 200), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FileRecord>> GetById(Guid id, CancellationToken ct)
        => (await _filesService.GetAsync(id, ct)) is { } rec ? Ok(rec) : NotFound();

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var rec = await _filesService.GetAsync(id, ct);
        if (rec is null) return NotFound();
        var stream = await _filesService.DownloadAsync(id, ct);
        return File(stream, rec.ContentType, rec.FileName);
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] Dictionary<string,string> metadata, CancellationToken ct)
    {
        await _filesService.UpdateMetadataAsync(id, metadata, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _filesService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("presigned-upload-url")]
    public async Task<ActionResult<string>> GetPresignedUploadUrl(string fileName, CancellationToken ct)
        => Ok(await _filesService.GetPresignedUploadUrlAsync(fileName, ct));
}
