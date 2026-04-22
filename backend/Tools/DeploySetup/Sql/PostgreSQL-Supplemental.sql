-- Orleans 10.1.0 AdoNet clustering requires CleanupDefunctSiloEntriesKey but the
-- upstream PostgreSQL-Clustering.sql does not populate it. Insert it idempotently
-- so existing databases (bootstrapped before this fix) pick it up on next migration.
-- Status = 6 is SiloStatus.Dead.
INSERT INTO OrleansQuery (QueryKey, QueryText)
VALUES (
    'CleanupDefunctSiloEntriesKey',
    'DELETE FROM OrleansMembershipTable
     WHERE DeploymentId = @DeploymentId
       AND @DeploymentId IS NOT NULL
       AND IAmAliveTime < @IAmAliveTime
       AND Status = 6;'
)
ON CONFLICT (QueryKey) DO NOTHING;
