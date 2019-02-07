using System.Text;
using System.Xml;
using System;
using System.Threading;

namespace Gate
{
    public class CmdTwo : Operation
    {
        public CmdTwo(string cmdName, string transactNum) : base(cmdName, transactNum)
        {
            
        }

        protected override void CheckTransactionValues()
        {

        }

        protected override void PrepareRequest()
        {
            
        }

        protected override void SendRequest()
        {
            Thread.Sleep(3500);
        }        

        protected override void ParseResponse()
        {
           
        }
    }
}
