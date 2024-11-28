namespace Images;

public class ImageAPI : IImageAPI
{
    public ImageAPI(IImageRepository repository)
    {
        _repository = repository;
    }

    private readonly IImageRepository _repository;

    public Task Refresh()
    {
        return _repository.Refresh();
    }

    public ImageData GetNext(ImageRequest request)
    {
        return _repository.GetNext(request.Index);
    }
}