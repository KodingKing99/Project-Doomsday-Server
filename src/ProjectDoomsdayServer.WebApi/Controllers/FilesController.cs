using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Files;
using Microsoft.AspNetCore.Mvc;

namespace ProjectDoomsdayServer.WebApi.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly FileService _svc;
    public FilesController(FileService svc) => _svc = svc;

    [HttpPost]
    [RequestSizeLimit(524_288_000)]
    public async Task<ActionResult<FileRecord>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        await using var stream = file.OpenReadStream();
        var rec = await _svc.UploadAsync(file.FileName, file.ContentType ?? "application/octet-stream", stream, ct);
        return CreatedAtAction(nameof(GetById), new { id = rec.Id }, rec);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileRecord>>> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _svc.ListAsync(skip, Math.Clamp(take, 1, 200), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FileRecord>> GetById(Guid id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } rec ? Ok(rec) : NotFound();

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var rec = await _svc.GetAsync(id, ct);
        if (rec is null) return NotFound();
        var stream = await _svc.DownloadAsync(id, ct);
        return File(stream, rec.ContentType, rec.FileName);
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] Dictionary<string,string> metadata, CancellationToken ct)
    {
        await _svc.UpdateMetadataAsync(id, metadata, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
