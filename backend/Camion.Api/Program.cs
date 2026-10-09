var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Endpoint técnico para comprobar que la API se ejecuta.
// No implementa ninguna función comercial.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
