using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure.Orleans;

public class TransactionParticipants
{
    public TransactionParticipants(TransactionInfo info)
    {
        Info = info;
    }

    private KeyValuePair<ParticipantId, AccessCounter> _manager;

    private readonly List<KeyValuePair<ParticipantId, AccessCounter>> _resources = new();

    public readonly TransactionInfo Info;
    public readonly List<ParticipantId> Write = new();

    public KeyValuePair<ParticipantId, AccessCounter> Manager => _manager;
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
                    _manager = participant;
                    priorityManager = _manager;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(Info.Participants),
                        "Only one priority transaction manager allowed in transaction"
                    );
                }
            }

            if (id.IsResource())
            {
                _resources.Add(participant);

                if (participant.Value.Writes > 0)
                    Write.Add(id);
            }

            if (manager == null && id.IsManager() == true && participant.Value.Writes > 0)
            {
                manager = participant;
                _manager = participant;
            }
        }
    }
}