using System;
using System.Xml.Linq;
using System.Linq;
using System.Xml;
using System.Text;
using System.ServiceModel;
using System.Net;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace Gate
{
    public abstract class Operation
    {
        private Config config;
        private string cmdName;

        #region Initialization new instance

        public Operation(string cmdName, string transactNum)
        {
            Authentication authentication = new Authentication();
            this.cmdName = cmdName;
            config = authentication.GetAuthData();
        }

        #endregion

        #region Properties

        public Config Config
        {
            get
            {
                return config;
            }
        }

        #endregion

        #region Abstract methods

        protected abstract void CheckTransactionValues();
        
        protected abstract void PrepareRequest();
        
        protected abstract void SendRequest();
        
        protected abstract void ParseResponse();

        #endregion

        #region Operation methods

        public void Start()
        {
            try
            {
                CheckTransactionValues();
                PrepareRequest();
                SendRequest();
                ParseResponse();
                MakeAnswer();
            }
            catch(Exception)
            {
                throw new Exception($"Exception in GateOne");
            }
        }


        private void MakeAnswer()
        {
            
        }

#endregion
    }
}