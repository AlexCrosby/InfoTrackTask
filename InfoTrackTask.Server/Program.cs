using InfoTrackTask.Server.Data;
using InfoTrackTask.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

var keepAliveConnection = new SqliteConnection("Data Source=:memory:");
keepAliveConnection.Open();
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlite(keepAliveConnection));

// Explicitly resolve the factory dependencies alongside the HttpClient
builder.Services.AddHttpClient<ISolicitorScraper, SolicitorScraper>(client =>
    {
        client.BaseAddress = new Uri("https://www.solicitors.com/");
    })
    .AddTypedClient<ISolicitorScraper>((httpClient, sp) => 
    {
        // Manually pull the logger and data context from the Service Provider (sp)
        var logger = sp.GetRequiredService<ILogger<SolicitorScraper>>();
        var context = sp.GetRequiredService<DataContext>();
    
        // Return the fully instantiated scraper class
        return new SolicitorScraper(httpClient, logger, context);
    });
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    context.Database.EnsureCreated(); // Builds your tables inside the SQLite RAM database
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");


app.Run();