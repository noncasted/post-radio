using System.Buffers.Text;
using System.Text;
using Common;
using Npgsql;

namespace Infrastructure.State;

public static class StateStorageExtensions
{
    extension(IStateStorage storage)
    {
        public Task<T> Read<T>(GrainId id) where T : IStateValue, new()
        {
            var stateInfo = storage.Registry.Get<T>();
            var stateIdentity = id.ToIdentity(stateInfo);
            return storage.Read<T>(stateIdentity);
        }

        public Task Write(StateIdentity identity, IStateValue value)
        {
            return storage.Write(new StateWriteRequest
            {
                Records = new Dictionary<StateIdentity, IStateValue> { { identity, value } }
            });
        }

        public Task Write(GrainId id, IStateValue value)
        {
            var stateInfo = storage.Registry.Get(value.GetType());
            var stateIdentity = id.ToIdentity(stateInfo);

            return storage.Write(new StateWriteRequest
            {
                Records = new Dictionary<StateIdentity, IStateValue> { { stateIdentity, value } }
            });
        }

        public Task Write(NpgsqlTransaction transaction, IReadOnlyDictionary<StateIdentity, IStateValue> records)
        {
            return storage.Write(new StateWriteRequest
            {
                Records = records,
                Transaction = transaction
            });
        }

        public Task Write(NpgsqlTransaction transaction, IReadOnlyList<GrainStateRecord> records)
        {
            var identityToRecord = new Dictionary<StateIdentity, IStateValue>();

            foreach (var record in records)
            {
                var stateInfo = storage.Registry.Get(record.Value.GetType());
                var identity = record.Id.ToIdentity(stateInfo);

                identityToRecord.Add(identity, record.Value);
            }

            return storage.Write(new StateWriteRequest
            {
                Records = identityToRecord,
                Transaction = transaction
            });
        }

        public Task Delete(StateIdentity identity)
        {
            return storage.Delete(new StateDeleteRequest { Identities = [identity] });
        }

        public Task Delete(IReadOnlyList<StateIdentity> identities)
        {
            return storage.Delete(new StateDeleteRequest { Identities = identities });
        }

        public Task Delete<T>(object key) where T : IStateValue, new()
        {
            var stateInfo = storage.Registry.Get<T>();

            var identity = new StateIdentity
            {
                Key = key,
                Type = stateInfo.Name,
                TableName = stateInfo.TableName,
                Extension = null
            };

            return storage.Delete(new StateDeleteRequest { Identities = [identity] });
        }

        public Task Delete<T>(IReadOnlyList<object> keys) where T : IStateValue, new()
        {
            var stateInfo = storage.Registry.Get<T>();

            var identities = keys.Select(key => new StateIdentity
                                 {
                                     Key = key,
                                     Type = stateInfo.Name,
                                     TableName = stateInfo.TableName,
                                     Extension = null
                                 })
                                 .ToList();

            return storage.Delete(new StateDeleteRequest { Identities = identities });
        }

        public Task<IReadOnlyDictionary<TKey, TValue>> Read<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
            where TKey : notnull
            where TValue : IStateValue, new()
        {
            return storage.ReadBatch<TKey, TValue>(identities);
        }
    }

    public static StateIdentity ToIdentity(this GrainId grainId, GrainStateInfo stateInfo)
    {
        var span = grainId.Key.AsSpan();

        switch (stateInfo.KeyType)
        {
            case GrainKeyType.Integer:
            {
                if (Utf8Parser.TryParse(span, out long key, out _, 'X') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as long.");

                return new StateIdentity
                {
                    Key = key,
                    Type = stateInfo.Name,
                    Extension = null,
                    TableName = stateInfo.TableName
                };
            }
            case GrainKeyType.String:
            {
                return new StateIdentity
                {
                    Key = grainId.Key.ToString(),
                    Type = stateInfo.Name,
                    Extension = null,
                    TableName = stateInfo.TableName
                };
            }
            case GrainKeyType.Guid:
            {
                if (Utf8Parser.TryParse(span, out Guid key, out _, 'N') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as Guid.");

                return new StateIdentity
                {
                    Key = key,
                    Type = stateInfo.Name,
                    Extension = null,
                    TableName = stateInfo.TableName
                };
            }
            case GrainKeyType.IntegerAndString:
            {
                var index = span.IndexOf((byte)'+');
                var extension = Encoding.UTF8.GetString(span[(index + 1)..]);
                var keySpan = span[..index];

                if (Utf8Parser.TryParse(keySpan, out long key, out _, 'X') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as long.");

                return new StateIdentity
                {
                    Key = key,
                    Type = stateInfo.Name,
                    Extension = extension,
                    TableName = stateInfo.TableName
                };
            }
            case GrainKeyType.GuidAndString:
            {
                var extension = Encoding.UTF8.GetString(span[33..]);
                var keySpan = span[..32];

                if (Utf8Parser.TryParse(keySpan, out Guid key, out _, 'N') == false)
                    throw new Exception($"Failed to parse grain key {grainId} as Guid.");

                return new StateIdentity
                {
                    Key = key,
                    Type = stateInfo.Name,
                    Extension = extension,
                    TableName = stateInfo.TableName
                };
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

    }
}