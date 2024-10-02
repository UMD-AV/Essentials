namespace PepperDash.Essentials.EpiphanPearl.Interfaces
{
    internal interface IEpiphanPearlClient
    {
        string Delete(string path);
        T Get<T>(string path) where T : class;

        TResponse Post<TBody, TResponse>(string path, TBody body)
            where TBody : class
            where TResponse : class;

        TResponse Post<TResponse>(string path) where TResponse : class;
    }
}