using Audio;
using Core;
using Images;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.AddCredentials();
builder.ConfigureCors();

builder
    .AddDefaultServices()
    .AddAudioServices()
    .AddImageServices();

builder.Services.AddHostedService<CoreStartup>();

var app = builder.Build();

app.UseCors("cors");
app.AddEndpoints(); 

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();