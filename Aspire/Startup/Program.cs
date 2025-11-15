using Aspire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder();
builder.AddServiceDefaults();

builder.Services.AddHostedService<ProjectStartup>();

var app = builder.Build();

await app.RunAsync();