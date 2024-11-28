using System;
using Global.Inputs;
using Internal;

namespace Global.GameServices
{
    public class LocalUser : ILocalUser
    {
        public LocalUser(Guid id, IUserInput input)
        {
            _id = id;
            _input = input;
        }   
     
        private readonly Guid _id;
        private readonly IUserInput _input;

        public Guid Id => _id;
        public IUserInput Input => _input; 
        public IReadOnlyLifetime Lifetime => _input.Lifetime;
    }
}