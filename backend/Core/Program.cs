using Audio;
using Core;
using Images;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("http://localhost:63342") 
            .AllowAnyMethod() 
            .AllowAnyHeader() 
            .AllowCredentials(); 
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.AddCredentials();

builder
    .AddDefaultServices()
    .AddAudioServices()
    .AddImageServices();

builder.Services.AddHostedService<CoreStartup>();

var app = builder.Build();

app.UseCors("AllowSpecificOrigin");
app.AddEndpoints(); 

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();