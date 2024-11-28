namespace Images;

public interface IImageAPI
{
    Task Refresh();
    ImageData GetNext(ImageRequest request);

}