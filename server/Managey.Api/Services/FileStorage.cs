namespace Managey.Api.Services;

public class FileStorage
{
    public const long MaxFileSize = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv",
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".zip",
    };

    private readonly string _root;

    public FileStorage(IConfiguration config, IWebHostEnvironment env)
    {
        _root = Path.GetFullPath(config["Storage:Path"] ?? "data/files", env.ContentRootPath);
        Directory.CreateDirectory(_root);
    }

    public static string? Validate(IFormFile file)
    {
        if (file.Length == 0) return "File is empty.";
        if (file.Length > MaxFileSize) return "File is larger than 20 MB.";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName))) return "File type is not allowed.";
        return null;
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(_root, storedName));
        await file.CopyToAsync(stream);
        return storedName;
    }

    public string PathFor(string storedName) => Path.Combine(_root, Path.GetFileName(storedName));

    public void Delete(string storedName)
    {
        var path = PathFor(storedName);
        if (File.Exists(path)) File.Delete(path);
    }
}
