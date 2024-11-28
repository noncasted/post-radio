using System;
using Global.Inputs;
using Internal;

namespace Global.GameServices
{
    public class LocalUsersService : IScopeSetup
    {
        public LocalUsersService(IInputView inputView, LocalUserList list)
        {
            _inputView = inputView;
            _list = list;
        }

        private readonly IInputView _inputView;
        private readonly LocalUserList _list;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _inputView.UserConnected.Advise(lifetime, OnUserConnected);
        }

        private void OnUserConnected(IUserInput input)
        {
            var id = Guid.NewGuid();
            var localUser = new LocalUser(id, input);
            _list.Add(localUser);
            input.Lifetime.Listen(() => _list.Remove(localUser));
        }
    }
}