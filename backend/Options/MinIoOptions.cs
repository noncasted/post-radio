namespace Options;

public class MinioOptions
{
    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    
    public required string ImagesBucket { get; init; }
    public required string AudioBucket { get; init; }
}