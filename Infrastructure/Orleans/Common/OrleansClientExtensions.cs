using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Runtime.Hosting;

namespace Infrastructure.Orleans;

public static class OrleansClientExtensions
{
    public static IHostApplicationBuilder AddOrleansClient(this IHostApplicationBuilder builder)
    {
        builder.UseOrleansClient(clientBuilder =>
            {
                var postgresConnectionString =
                    clientBuilder.Configuration.GetConnectionString(ConnectionNames.Postgres)!;

                clientBuilder.UseTransactions();
                
                clientBuilder.Configure<MessagingOptions>(options =>
                    {
                        options.ResponseTimeout = TimeSpan.FromSeconds(5);
                        options.ResponseTimeoutWithDebugger = TimeSpan.FromSeconds(5);
                    }
                );


                if (builder.Environment.IsDevelopment() == true)
                {
                    clientBuilder.UseLocalhostClustering();
                }
                else
                {
                    clientBuilder.UseAdoNetClustering(options =>
                        {
                            options.Invariant = "Npgsql";
                            options.ConnectionString = postgresConnectionString;
                        }
                    );
                }

                clientBuilder.UseConnectionRetryFilter((_, _) => Task.FromResult(true));
            }
        );

        return builder;
    }

    public static IHostApplicationBuilder ConfigureSilo(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        TransactionalStateOptions.DefaultLockTimeout = TimeSpan.FromSeconds(5);

        builder.UseOrleans(siloBuilder =>
            {
                var npgsqlConnectionString = configuration.GetConnectionString(ConnectionNames.Postgres)!;

                siloBuilder.UseTransactions();
                siloBuilder.Configure<MessagingOptions>(options =>
                    {
                        options.ResponseTimeout = TimeSpan.FromSeconds(5);
                        options.ResponseTimeoutWithDebugger = TimeSpan.FromSeconds(5);
                    }
                );

                if (builder.Environment.IsDevelopment() == true)
                {
                    siloBuilder.UseLocalhostClustering();
                }
                else
                {
                    siloBuilder.UseAdoNetClustering(options =>
                        {
                            options.Invariant = "Npgsql";
                            options.ConnectionString = npgsqlConnectionString;
                        }
                    );
                }

                siloBuilder.AddAdoNetGrainStorageAsDefault(options =>
                    {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = npgsqlConnectionString;
                    }
                );

                foreach (var name in States.StateTables)
                {
                    siloBuilder.Services.AddGrainStorage(name,
                        (s, _) => NamedGrainStorageFactory.Create(s, name, npgsqlConnectionString)
                    );
                }

                siloBuilder.AddActivityPropagation();
            }
        );

        return builder;
    }
}