using Common;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DeploySetup;

public class StatesSetup
{
    public static async Task Run(IConfigurationManager configuration)
    {
        await using var connection = await configuration.GetConnection();

        foreach (var info in StatesLookup.All)
        {
            if (await connection.IsTableExists(info.TableName) == true)
                continue;

            string key;
            string index;

            switch (info.KeyType)
            {
                case GrainKeyType.Integer:
                    key = "bigint not null";
                    index = "(key, type)";
                    break;
                case GrainKeyType.String:
                    key = "character varying(512) not null";
                    index = "(key, type)";

                    break;
                case GrainKeyType.Guid:
                    key = "uuid not null";
                    index = "(key, type)";

                    break;
                case GrainKeyType.IntegerAndString:
                    key = "bigint not null, extension character varying(512) not null";
                    index = "(key, type, extension)";

                    break;
                case GrainKeyType.GuidAndString:
                    key = "uuid not null, extension character varying(512) not null";
                    index = "(key, type, extension)";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var createTableQuery = $@"
                    CREATE TABLE {info.TableName} (
                        key {key} ,
                        type character varying(512) not null,
                        value jsonb NOT NULL,
                        version int NOT NULL,
                        primary key {index}
                    );

                    CREATE INDEX ix_{info.TableName}
                        ON {info.TableName} USING btree
                        {index};
                    ";

            await using var createTableCommand = new NpgsqlCommand(createTableQuery, connection);
            await createTableCommand.ExecuteNonQueryAsync();
        }
    }
}