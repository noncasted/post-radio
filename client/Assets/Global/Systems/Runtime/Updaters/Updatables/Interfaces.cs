namespace Global.Systems
{
    public interface IFixedUpdatable
    {
        void OnFixedUpdate(float delta);
    }
    
    public interface IGizmosUpdatable
    {
        void OnGizmosUpdate();
    }
    
    public interface IPostFixedUpdatable
    {
        void OnPostFixedUpdate(float delta);
    }
    
    public interface IPreFixedUpdatable
    {
        void OnPreFixedUpdate(float delta);
    }
    
    public interface IPreUpdatable
    {
        void OnPreUpdate(float delta);
    }
    
    public interface IUpdatable
    {
        void OnUpdate(float delta);
    }
}