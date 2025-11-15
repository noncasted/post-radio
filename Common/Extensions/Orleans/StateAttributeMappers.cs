using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Common;

public class GenericTransactionalStateAttributeMapper<TAttribute> : TransactionalStateAttributeMapper<TAttribute>
    where TAttribute : IFacetMetadata, ITransactionalStateConfiguration
{
    protected override TransactionalStateConfiguration AttributeToConfig(TAttribute attribute)
    {
        return new TransactionalStateConfiguration(attribute);
    }
}

public class GenericPersistentStateAttributeMapper<TAttribute> : IAttributeToFactoryMapper<TAttribute>
    where TAttribute : IFacetMetadata, IPersistentStateConfiguration

{
    private readonly MethodInfo _createMethodInfo = typeof(IPersistentStateFactory).GetMethod("Create")!;

    public Factory<IGrainContext, object> GetFactory(ParameterInfo parameter, TAttribute attribute)
    {
        IPersistentStateConfiguration config = attribute;

        if (string.IsNullOrEmpty(config.StateName) == true)
        {
            config = new PersistentStateConfiguration()
                { StateName = parameter.Name!, StorageName = attribute.StorageName };
        }

        var parameterType = parameter.ParameterType;

        if (!parameterType.IsGenericType || typeof(IPersistentState<>) != parameterType.GetGenericTypeDefinition())
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' on the constructor for '{parameter.Member.DeclaringType}' has an unsupported type, '{parameterType}'. "
                + $"It must be an instance of generic type '{typeof(IPersistentState<>)}' because it has an associated [PersistentState(...)] attribute.",
                parameter.Name);
        }

        var genericCreate = _createMethodInfo.MakeGenericMethod(parameterType.GetGenericArguments());
        return context => Create(context, genericCreate, config);
    }

    private static object Create(IGrainContext context, MethodInfo genericCreate, IPersistentStateConfiguration config)
    {
        var factory = context.ActivationServices.GetRequiredService<IPersistentStateFactory>();
        object[] args = [context, config];
        return genericCreate.Invoke(factory, args)!;
    }

    private class PersistentStateConfiguration : IPersistentStateConfiguration
    {
        public required string StateName { get; init; }

        public required string StorageName { get; init; }
    }
}