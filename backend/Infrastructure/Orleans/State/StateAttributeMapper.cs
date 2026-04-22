using System.Reflection;
using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.State;

public class StateAttributeMapper : IAttributeToFactoryMapper<StateAttribute>
{
    private readonly MethodInfo _createMethodInfo = typeof(IStateFactory).GetMethod("Create").ThrowIfNull();

    public Factory<IGrainContext, object> GetFactory(ParameterInfo parameter, StateAttribute attribute)
    {
        var parameterType = parameter.ParameterType;

        if (!parameterType.IsGenericType || typeof(State<>) != parameterType.GetGenericTypeDefinition())
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' on the constructor for '{parameter.Member.DeclaringType}' has an unsupported type, '{parameterType}'. " +
                $"It must be an instance of generic type '{typeof(State<>)}' because it has an associated [State] attribute.",
                parameter.Name);
        }

        var genericCreate = _createMethodInfo.MakeGenericMethod(parameterType.GetGenericArguments());
        return context => Create(context, genericCreate);
    }

    private static object Create(IGrainContext context, MethodInfo genericCreate)
    {
        var factory = context.ActivationServices.GetRequiredService<IStateFactory>();
        object[] args = [context];
        return genericCreate.Invoke(factory, args).ThrowIfNull();
    }
}

public interface IStateFactory
{
    State<TState> Create<TState>(IGrainContext context) where TState : class, IStateValue, new();
}

public class StateFactory : IStateFactory
{
    public StateFactory(IStateStorage stateStorage, IStateSerializer serializer)
    {
        _stateStorage = stateStorage;
        _serializer = serializer;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IStateSerializer _serializer;

    public State<TState> Create<TState>(IGrainContext context) where TState : class, IStateValue, new()
    {
        return new State<TState>(_stateStorage, context, _serializer);
    }
}