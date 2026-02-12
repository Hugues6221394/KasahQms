var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "KasahQMS API is running.");
app.Run();
