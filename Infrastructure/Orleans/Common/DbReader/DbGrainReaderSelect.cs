namespace Infrastructure.Orleans;

public class DbGrainReaderSelect
{
    public bool Id { get; set; }
    public bool Payload { get; set; }
    public bool Extension { get; set; }
    
    public string FormQuery() {
        var entries = new List<string>();

        if (Id == true)
            entries.Add("id_0, id_1");

        if (Payload == true)
            entries.Add("payload");

        if (Extension == true)
            entries.Add("extension");
        
        if (entries.Count == 0)
            entries.Add("*");

        return string.Join(", ", entries);
    }

    public void Validate()
    {
        if (Id == false && Payload == false && Extension == false)
        {
            Id = true;
            Payload = true;
            Extension = true;
        }
    }
}