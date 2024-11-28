namespace Images;

public interface IImageRepository
{
    Task Refresh();
    Task<ImageData> GetNext(int current);
}