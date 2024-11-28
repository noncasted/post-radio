namespace Internal
{
    public interface IGlobalLogger
    {
        void Log(string message);
        void Log(string header, string message);
        void Log(string message, ILogParameters parameters);
        void Warning(string warning);
        void Error(string error);
        void Error(string error, ILogParameters parameters);
    }
}