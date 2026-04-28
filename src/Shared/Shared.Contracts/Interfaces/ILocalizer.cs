namespace Shared.Contracts.Interfaces
{
    public interface ILocalizer
    {
        string Get(string key, params object[] args);
    }
}
