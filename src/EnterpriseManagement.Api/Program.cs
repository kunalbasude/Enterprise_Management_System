using EnterpriseManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root. One call per layer: Program.cs stays a wiring file and never
// learns what database or ORM is in use.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Exposed so the integration test project can reference this entry point with
/// WebApplicationFactory. Top-level statements generate an internal Program class.
/// </summary>
public partial class Program;
