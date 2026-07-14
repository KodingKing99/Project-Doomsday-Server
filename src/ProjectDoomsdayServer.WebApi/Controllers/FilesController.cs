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
    [ProducesResponseType<CreateFileResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        var result = await _filesService.CreateAsync(
            new CreateFileRequest { Input = record, AuthenticatedUserId = userId },
            cancellationToken
        );
        return CreatedAtAction(nameof(GetById), new { id = result.File.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType<File>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<File>> Update(
        string id,
        [FromBody] File record,
        CancellationToken cancellationToken
    )
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
            return BadRequest("User must be authenticated");

        if (string.IsNullOrEmpty(record.FileName))
            return BadRequest("FileName is required.");

        var result = await _filesService.UpdateAsync(
            new UpdateFileRequest
            {
                Id = id,
                AuthenticatedUserId = userId,
                FileName = record.FileName,
                ContentType = record.ContentType,
                SizeBytes = record.SizeBytes,
                HashSha256 = record.HashSha256,
                Metadata = record.Metadata ?? new(),
            },
            cancellationToken
        );
        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<File>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType<File>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<File>> GetById(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
        {
            return BadRequest("User must be authenticated");
        }
        var file = await _filesService.GetAsync(
            new GetFileRequest { Id = id, AuthenticatedUserId = userId },
            cancellationToken
        );
        return Ok(file);
    }

    [HttpGet("{id}/content")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
            return BadRequest("User must be authenticated");

        var fileMetadata = await _filesService.GetAsync(
            new GetFileRequest { Id = id, AuthenticatedUserId = userId },
            cancellationToken
        );

        var stream = await _filesService.DownloadAsync(
            new DownloadFileRequest { Id = id, AuthenticatedUserId = userId },
            cancellationToken
        );
        return File(stream, fileMetadata.ContentType, fileMetadata.FileName ?? fileMetadata.Id);
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub");
        if (userId is null)
            return BadRequest("User must be authenticated");

        await _filesService.DeleteAsync(
            new DeleteFileRequest { Id = id, AuthenticatedUserId = userId },
            cancellationToken
        );
        return NoContent();
    }
}
