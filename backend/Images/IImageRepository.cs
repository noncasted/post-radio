namespace Images;

public interface IImageRepository
{
    Task Refresh();
    ImageData GetNext(int current);
}