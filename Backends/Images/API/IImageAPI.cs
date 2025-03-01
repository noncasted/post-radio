namespace Images;

public interface IImageAPI
{
    Task Refresh();
    Task<ImageData> GetNext(ImageRequest request);
}