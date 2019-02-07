namespace Additional
{
    public interface IPlugin
    {
        int Count { get; }

        string How { get; }
        string HowCode { get; }
        void StartOperation(string command, string transactNum);
        IPlugin CreateNewInstance();
        void Destroy();
    }
}
