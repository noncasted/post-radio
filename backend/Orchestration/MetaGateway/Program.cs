using MetaGateway;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupMetaGateway();

var app = builder.Build();

app.UseCors("cors");
app.AddRadioEndpoints();
app.MapDefaultEndpoints();

app.Run();