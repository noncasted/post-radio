namespace Images;

public interface IImageRepository
{
    Task Run();
    Task Refresh();
    Task<ImageData> GetNext(int current);
}