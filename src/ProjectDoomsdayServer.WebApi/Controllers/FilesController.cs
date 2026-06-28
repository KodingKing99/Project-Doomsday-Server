using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Models.Input;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.WebApi.Controllers;

[ApiController]
[Route("files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFilesService _filesService;

    public FilesController(IFilesService filesService) => _filesService = filesService;

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateFileResult>> Create(
        [FromBody] CreateFileInput record,
        CancellationToken cancellationToken
    )
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
        {
            return BadRequest("User not found");
        }
        var result = await _filesService.CreateAsync(record, userId, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = result.File.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<File>> Update(
        string id,
        [FromBody] File record,
        CancellationToken cancellationToken
    )
    {
        var existing = await _filesService.GetAsync(id, cancellationToken);
        if (existing is null)
            return NotFound();

        record.Id = id;
        var result = await _filesService.UpdateAsync(record, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<File>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
        {
            return BadRequest("User must be authenticated");
        }
        take = Math.Clamp(take, 1, 200);
        return Ok(
            await _filesService.ListAsync(
                new ListFileRequest
                {
                    AuthenticatedUserId = userId,
                    Take = take,
                    Skip = skip,
                },
                cancellationToken
            )
        );
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<File>> GetById(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
        {
            return BadRequest("User must be authenticated");
        }
        var file = await _filesService.GetAsync(id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }
        return file;
    }

    [HttpGet("{id}/content")]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        var rec = await _filesService.GetAsync(id, cancellationToken);
        if (rec is null)
            return NotFound();
        var stream = await _filesService.DownloadAsync(id, cancellationToken);
        return File(stream, rec.ContentType, rec.FileName);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _filesService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
