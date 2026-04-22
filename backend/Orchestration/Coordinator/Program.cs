using Common.Extensions;
using Coordinator;
using Infrastructure;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupCoordinator();

builder.Services.Add<ClusterCoordinator>()
       .As<ILocalSetupCompleted>();

builder.Services.AddHostedService<DeployIdentity>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();