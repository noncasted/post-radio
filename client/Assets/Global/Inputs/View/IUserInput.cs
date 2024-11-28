using Internal;

namespace Global.Inputs
{
    public interface IUserInput
    {
        Controls Controls { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}