// Program.cs is the entry point of an ASP.NET Core app.
// For a frontend dev: think of it as server.js in Express —
// it wires up middleware, services, and routes, then starts listening.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "URL Shortener POC - wiring coming in Task 16");
app.Run();
