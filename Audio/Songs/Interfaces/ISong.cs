namespace Audio;

public interface ISong : IGrainWithIntegerKey
{
    Task UpdateData(SongData data);
    Task<SongData> GetData();
    Task AddToPlaylist(Guid playlistId);
    Task RemoveFromPlaylist(Guid playlistId);
}