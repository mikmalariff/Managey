var builder = WebApplication.CreateBuilder(args);

// Allows running as a Windows Service (no-op when run from a console).
builder.Host.UseWindowsService(options => options.ServiceName = "Managey");

builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

app.Run();
