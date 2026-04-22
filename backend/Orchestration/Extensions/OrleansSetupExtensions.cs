using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;

namespace Orchestration;

public static class OrleansSetupExtensions
{
    public static readonly TimeSpan ReplyTimeoutSeconds = TimeSpan.FromSeconds(5);

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddOrleansClient()
        {
            builder.UseOrleansClient(clientBuilder => {
                var postgresConnectionString = clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)
                                                            .ThrowIfNull();

                clientBuilder.Configure<ClientMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    clientBuilder.UseLocalhostClustering();
                }
                else
                {
                    clientBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = postgresConnectionString;
                    });
                }

                clientBuilder.UseConnectionRetryFilter((_, _) => Task.FromResult(true));
            });

            return builder;
        }

        public IHostApplicationBuilder ConfigureSilo()
        {
            var configuration = builder.Configuration;

            builder.UseOrleans(siloBuilder => {
                var npgsqlConnectionString = configuration.GetConnectionString(ConnectionNames.Postgres).ThrowIfNull();

                siloBuilder.Configure<SiloMessagingOptions>(options => {
                    options.ResponseTimeout = ReplyTimeoutSeconds;
                    options.ResponseTimeoutWithDebugger = ReplyTimeoutSeconds * 10;
                });

                if (builder.Environment.IsDevelopment() == true)
                {
                    siloBuilder.UseLocalhostClustering();
                }
                else
                {
                    siloBuilder.UseAdoNetClustering(options => {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = npgsqlConnectionString;
                    });
                }

                siloBuilder.AddActivityPropagation();

                siloBuilder.AddGrainExtension<IGrainTransactionHandler, GrainTransactionHandler>();
            });

            return builder;
        }
    }
}