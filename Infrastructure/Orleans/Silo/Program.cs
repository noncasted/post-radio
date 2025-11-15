using Aspire;
using Common;

var builder = WebApplication.CreateBuilder(args);

builder.SetupSilo();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();