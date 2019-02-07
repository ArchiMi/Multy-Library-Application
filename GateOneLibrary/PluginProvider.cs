using Additional;
using System;
using System.ComponentModel.Composition;

namespace Gate
{
    [Export(typeof(IPlugin))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PluginProvider: IPlugin
    {
        private Operation operation;

        public IPlugin CreateNewInstance()
        {
            return new PluginProvider();
        }

        public void StartOperation(string command, string transactNum)
        {
            switch (command)
            {
                case "cmd_one":
                    operation = new CmdOne(transactNum, command);
                    break;
                default:
                    throw new Exception($"Uncnown error!");
            }
            operation.Start();
        }

        public void Destroy()
        {
            
        }

        public string How
        {
            get { return Config.how; }
        }

        public string HowCode
        {
            get { return Config.howCode; }
        }

        public int Count => throw new NotImplementedException();
    }
}
