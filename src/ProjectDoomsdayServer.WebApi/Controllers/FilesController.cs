using Microsoft.AspNetCore.Mvc;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.WebApi.Controllers;

[ApiController]
[Route("files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFilesService _filesService;

    public FilesController(IFilesService filesService) => _filesService = filesService;

    [HttpPost]
    public async Task<ActionResult<File>> Create([FromBody] File record, CancellationToken ct)
    {
        record.Id = null;
        var result = await _filesService.CreateAsync(record, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<File>> Update(
        string id,
        [FromBody] File record,
        CancellationToken ct
    )
    {
        var existing = await _filesService.GetAsync(id, ct);
        if (existing is null)
            return NotFound();

        record.Id = id;
        var result = await _filesService.UpdateAsync(record, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<File>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    ) => Ok(await _filesService.ListAsync(skip, Math.Clamp(take, 1, 200), ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<File>> GetById(string id, CancellationToken ct) =>
        (await _filesService.GetAsync(id, ct)) is { } rec ? Ok(rec) : NotFound();

    [HttpGet("{id}/content")]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var rec = await _filesService.GetAsync(id, ct);
        if (rec is null)
            return NotFound();
        var stream = await _filesService.DownloadAsync(id, ct);
        return File(stream, rec.ContentType, rec.FileName);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _filesService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("presigned-upload-url")]
    public async Task<ActionResult<string>> GetPresignedUploadUrl(
        string fileName,
        CancellationToken ct
    ) => Ok(await _filesService.GetPresignedUploadUrlAsync(fileName, ct));
}
