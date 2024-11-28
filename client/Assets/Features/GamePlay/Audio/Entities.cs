namespace GamePlay.Audio
{
    public class TrackData
    {
        public string DownloadUrl { get; set; }
        public SongMetadata Metadata { get; set; }
    }

    public class SongMetadata
    {
        public string Url { get; set; }
        public string Author { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
    }
    
    public class GetNextTrackRequest
    {
        public int Index { get; set; }
    }
}