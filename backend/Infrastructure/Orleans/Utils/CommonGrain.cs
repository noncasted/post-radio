namespace Infrastructure;

public interface ICommonGrain
{
    IGrainFactory Grains { get; }
    GrainReference Reference { get; }
}

public class CommonGrain : Grain, ICommonGrain
{
    public string StringId => this.GetPrimaryKeyString();
    public IGrainFactory Grains => GrainFactory;
    public GrainReference Reference => GrainReference;
}