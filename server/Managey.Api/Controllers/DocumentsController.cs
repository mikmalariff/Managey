using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

/// <summary>Company document library (policies, forms, handbooks) plus downloads of request attachments.</summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController(AppDbContext db, AuditService audit, FileStorage files) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.Documents
        .Where(d => d.RequestId == null)
        .OrderBy(d => d.Category).ThenBy(d => d.FileName)
        .Select(d => new { d.Id, d.FileName, d.Category, d.Size, d.UploadedAt, UploadedBy = d.UploadedBy!.FullName })
        .ToListAsync());

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrHR)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? category)
    {
        if (FileStorage.Validate(file) is { } error) return BadRequest(new { message = error });
        var doc = new Document
        {
            FileName = Path.GetFileName(file.FileName),
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            StoredName = await files.SaveAsync(file),
            ContentType = file.ContentType,
            Size = file.Length,
            UploadedById = User.UserId(),
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        audit.Log("Upload", nameof(Document), doc.Id, doc.FileName);
        await db.SaveChangesAsync();
        return Ok(new { doc.Id });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        if (doc.RequestId is { } requestId)
        {
            var request = await db.Requests.Include(r => r.Steps).FirstAsync(r => r.Id == requestId);
            if (!RequestsController.CanView(request, User)) return Forbid();
        }
        var path = files.PathFor(doc.StoredName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, doc.ContentType, doc.FileName);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.RequestId == null);
        if (doc is null) return NotFound();
        db.Documents.Remove(doc);
        audit.Log("Delete", nameof(Document), id, doc.FileName);
        await db.SaveChangesAsync();
        files.Delete(doc.StoredName);
        return NoContent();
    }
}
