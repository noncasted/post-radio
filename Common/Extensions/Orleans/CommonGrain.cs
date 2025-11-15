namespace Common;

public interface ICommonGrain
{
    IGrainFactory Grains { get; }
}

public class CommonGrain : Grain, ICommonGrain
{
    public string StringId => this.GetPrimaryKeyString();
    public IGrainFactory Grains => GrainFactory;
}