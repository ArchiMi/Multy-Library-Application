using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultyLibraryApplication
{
    public class OperationResult : IDisposable
    {
        private string command;
        private string transact;
        private string how_point;
        
        public string Command
        {
            get { return this.command; }
        }

        public string Transact
        {
            get { return this.transact; }
        }

        public string HowPoint
        {
            get { return this.how_point; }
        }

        public OperationResult(string transact, string command, string how_point)
        {
            this.transact = transact;
            this.command = command;
            this.how_point = how_point;
        }

        protected virtual void Dispose(bool disposed)
        {
            if (disposed)
            {

            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OperationResult()
        {
            Dispose(false);
        }
    }
}
