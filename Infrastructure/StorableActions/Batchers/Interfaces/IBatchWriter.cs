namespace Infrastructure.StorableActions;

public interface IBatchWriter<T> : IGrainWithStringKey
{
    Task Start();
    
    Task Loop();
}