using Orleans.Storage;

namespace Infrastructure.Orleans;

public static class GrainStorageVersionGuard
{
    /// <summary>
    /// Checks for version inconsistency as defined in the database scripts.
    /// </summary>
    /// <param name="operation">The operation attempted.</param>
    /// <param name="storageVersion">The version from storage.</param>
    /// <param name="providerName">Table name</param>
    /// <param name="grainVersion">The grain version.</param>
    /// <param name="normalizedGrainType">Grain type without generics information.</param>
    /// <param name="grainId">The grain ID.</param>
    /// <returns>An exception for throwing or <em>null</em> if no violation was detected.</returns>
    /// <remarks>This means that the version was not updated in the database or the version storage version was something else than null
    /// when the grain version was null, meaning effectively a double activation and save.</remarks>
    public static void Check(
        string operation,
        string providerName,
        string storageVersion,
        string grainVersion,
        string normalizedGrainType,
        string grainId)
    {
        //If these are the same, it means no row was inserted or updated in the storage.
        //Effectively it means the UPDATE or INSERT conditions failed due to ETag violation.
        //Also if grainState.ETag storageVersion is null and storage comes back as null,
        //it means two grains were activated an the other one succeeded in writing its state.
        //
        //NOTE: the storage could return also the new and old ETag (Version), but currently it doesn't.
        if (storageVersion == grainVersion || storageVersion == string.Empty)
        {
            //TODO: Note that this error message should be canonical across back-ends.
            throw new InconsistentStateException(
                $"Version conflict ({operation}): ProviderName={providerName} GrainType={normalizedGrainType} GrainId={grainId} ETag={grainVersion}."
            );
        }
    }
}