using Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public static class AuditLogSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        var createSql = $@"
            CREATE TABLE audit_log (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                timestamp timestamptz NOT NULL DEFAULT now(),
                action text NOT NULL,
                details text NOT NULL DEFAULT '',
                PRIMARY KEY (id)
            );
            CREATE INDEX ix_audit_log_timestamp ON audit_log USING btree (timestamp DESC);
        ";

        await connection.CreateIfNotExists("audit_log", createSql);
    }
}