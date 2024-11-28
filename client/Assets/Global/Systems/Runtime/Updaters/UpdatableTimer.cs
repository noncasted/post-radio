namespace Global.Systems
{
    public class UpdatableTimer
    {
        public UpdatableTimer(float target)
        {
            _target = target;
        }

        private readonly float _target;

        private float _current;

        public bool Update(float delta)
        {
            _current += delta;

            if (_current >= _target)
                return true;

            return false;
        }

        public void Reset()
        {
            _current = 0;
        }
    }
}