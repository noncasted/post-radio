using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public static class ParticipantsExtensions
{
    public static ITransactionalResourceExtension AsResource(this ParticipantId participant)
    {
        if (!participant.SupportsRoles(ParticipantId.Role.Resource))
            throw new InvalidOperationException($"Participant {participant} does not support Resource role");

        return participant.Reference.AsReference<ITransactionalResourceExtension>();
    }
}