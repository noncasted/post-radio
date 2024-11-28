namespace Images.API;

public interface IImageAPI
{
    Task Refresh();
    ImageData GetNext(ImageRequest request);

}