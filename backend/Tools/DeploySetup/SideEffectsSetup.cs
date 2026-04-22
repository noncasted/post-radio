using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public static class SideEffectsSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        var queue = $@"
            CREATE TABLE {DbLookup.SE_Queue} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_{DbLookup.SE_Queue} ON {DbLookup.SE_Queue} USING btree (created_at);
        ";

        var processing = $@"
            CREATE TABLE {DbLookup.SE_Processing} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                processing_started_at timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
        ";

        var retry = $@"
            CREATE TABLE {DbLookup.SE_Retry} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                retry_after timestamptz NOT NULL,
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_{DbLookup.SE_Retry} ON {DbLookup.SE_Retry} USING btree (retry_after);
        ";

        var deadLetter = $@"
            CREATE TABLE {DbLookup.SE_DeadLetter} (
                id uuid NOT NULL,
                payload jsonb NOT NULL,
                retry_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                failed_at timestamptz NOT NULL DEFAULT now(),
                error_message text,
                PRIMARY KEY (id)
            );
        ";

        await connection.CreateIfNotExists(DbLookup.SE_Queue, queue);
        await connection.CreateIfNotExists(DbLookup.SE_Processing, processing);
        await connection.CreateIfNotExists(DbLookup.SE_Retry, retry);
        await connection.CreateIfNotExists(DbLookup.SE_DeadLetter, deadLetter);

    }
}