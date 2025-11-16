using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public class TransactionParticipants
{
    public TransactionParticipants(TransactionInfo info)
    {
        Info = info;
    }

    private readonly List<KeyValuePair<ParticipantId, AccessCounter>> _resources = new();

    public readonly TransactionInfo Info;
    public readonly List<ParticipantId> Write = new();

    public KeyValuePair<ParticipantId, AccessCounter> Manager { get; private set; }

    public IReadOnlyList<KeyValuePair<ParticipantId, AccessCounter>> Resources => _resources;

    public void Collect()
    {
        KeyValuePair<ParticipantId, AccessCounter>? priorityManager = null;
        KeyValuePair<ParticipantId, AccessCounter>? manager = null;

        foreach (var participant in Info.Participants)
        {
            var id = participant.Key;

            if (id.IsPriorityManager() == true)
            {
                if (priorityManager == null)
                {
                    Manager = participant;
                    priorityManager = Manager;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(Info.Participants),
                        "Only one priority transaction manager allowed in transaction"
                    );
                }
            }

            if (id.IsResource() == true)
            {
                _resources.Add(participant);

                if (participant.Value.Writes > 0)
                    Write.Add(id);
            }

            if (manager == null && id.IsManager() == true && participant.Value.Writes > 0)
            {
                manager = participant;
                Manager = participant;
            }
        }
    }
}