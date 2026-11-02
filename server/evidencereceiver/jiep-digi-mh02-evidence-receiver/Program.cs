var builder = WebApplication.CreateBuilder(args);


// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add controllers to the service container
builder.Services.AddControllers();

// Configure Kestrel to use the port from appsettings.json
builder.WebHost.ConfigureKestrel(options =>
{
    var kestrelSection = builder.Configuration.GetSection("PortSettings:Port");
    var port = kestrelSection.Get<int>();
    if (port > 0)
    {
        options.ListenAnyIP(port); // Set Kestrel to listen on the specified port
    }
    else
    {
        options.ListenAnyIP(5000); // Default to port 5000 if no valid port is found
    }
});

var app = builder.Build();

// Use routing and map controllers
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
