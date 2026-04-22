namespace Infrastructure.State;

[AttributeUsage(AttributeTargets.Parameter)]
public class StateAttribute : Attribute, IFacetMetadata
{
}