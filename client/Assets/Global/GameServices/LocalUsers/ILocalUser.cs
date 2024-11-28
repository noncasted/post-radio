using System;
using Global.Inputs;
using Internal;

namespace Global.GameServices
{
    public interface ILocalUser
    {
        Guid Id { get; }
        IUserInput Input { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}