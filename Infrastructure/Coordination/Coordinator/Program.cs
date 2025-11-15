using Common;
using Infrastructure.Coordination;
using ServiceLoop;

var builder = WebApplication.CreateBuilder(args);

builder.SetupCoordinator();

builder.Services.Add<ClusterCoordinator>()
    .As<ILocalSetupCompleted>();

var app = builder.Build();

app.Run();