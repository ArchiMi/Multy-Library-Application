using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Additional
{
    public class OperationValues : IDisposable
    {
        private string transact;
        private Dictionary<string, string> fields;
        private string how;
        private string how_point;
        private string id;
        private string command;
        private string fieldsHistory;
        private string unicData;
        private string tryCount;
        private DateTime nextTryDate;
        private Dictionary<string, string> resultText;
        private Dictionary<string, object> resultFields;

        public OperationValues()
        {
            this.fields = new Dictionary<string, string>();
            this.resultFields = new Dictionary<string, object>();
            this.resultText = new Dictionary<string, string>();
        }

        public string How
        {
            get { return how; }
            set { how = value; }
        }

        public string HowPoint
        {
            get { return how_point; }
            set { how_point = value; }
        }

        public string Id
        {
            get { return id; }
            set { id = value; }
        }

        public string Transact
        {
            get { return transact; }
            set { transact = value; }
        }

        public Dictionary<string, string> Fields
        {
            get { return fields; }
            set { fields = value; }
        }

        public string Command
        {
            get { return command; }
            set { command = value; }
        }

        public string FieldsHistory
        {
            get { return fieldsHistory; }
            set { fieldsHistory = value; }
        }

        public Dictionary<string, string> ResultText
        {
            get { return resultText; }
            set { resultText = value; }
        }

        public Dictionary<string, object> ResultFields
        {
            get { return resultFields; }
            set { resultFields = value; }
        }

        public string UnicData
        {
            get { return unicData; }
            set { unicData = value; }
        }

        public string TryCount
        {
            get { return tryCount; }
            set { tryCount = value; }
        }

        public DateTime NextTryDate
        {
            get { return nextTryDate; }
            set { nextTryDate = value; }
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

        ~OperationValues()
        {
            Dispose(false);
        }
    }
}
