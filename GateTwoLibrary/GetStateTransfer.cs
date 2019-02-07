using System.Text;
using System.Xml;
using System.Xml.Linq;
using Helper;
using Gate.ContactWebService;

namespace Gate
{
    public class GetStateTransfer: Operation
    {
        XmlDocument xDoc;
        string inXML;
        string outXML;

        public GetStateTransfer(TransactionValues trava) : base(trava) 
        {
            xDoc = new XmlDocument();
            trava.Fields["knp"] = trava.DocIDFromKnp;
        }

        protected override void CheckTransactionValues()
        {
           // CheckTransactionValues(trava, OperationCommands.GET_STATE_TRANSFER);
        }

        protected override void PrepareRequest()
        {
            XmlDeclaration xmlDeclaration = xDoc.CreateXmlDeclaration("1.0", "utf-8", null);
            XmlElement xREQUEST = xDoc.CreateElement("REQUEST");
            xREQUEST = xDoc.CreateElement("REQUEST");
            xDoc.InsertBefore(xmlDeclaration, xDoc.DocumentElement);
            xDoc.AppendChild(xREQUEST);
            AddAttribute(xDoc, xREQUEST, "OBJECT_CLASS", ActionsContact.REQUEST_OBJECT);
            AddAttribute(xDoc, xREQUEST, "ACTION", ActionsContact.GET_STATE_TRANSFER);
            AddAttribute(xDoc, xREQUEST, "DOC_ID", trava.Fields["knp"]);
            AddAttribute(xDoc, xREQUEST, "SERVICE_ID", "2");
            AddAttribute(xDoc, xREQUEST, "INT_SOFT_ID", Config.Guid);
            AddAttribute(xDoc, xREQUEST, "POINT_CODE", Config.PointCode);
            AddAttribute(xDoc, xREQUEST, "USER_ID", "2101");
            AddAttribute(xDoc, xREQUEST, "LANG", "RU");
            AddAttribute(xDoc, xREQUEST, "INOUT", "I");
            Log.WriteLog(xDoc.InnerXml, trava.Transact);
            inXML = Contact.Crypto.CreateAndSignMessage(Encoding.UTF8.GetBytes(xDoc.InnerXml), Encoding.Default.GetBytes(Config.PointCode), false, true);
        }

        protected override void SendRequest()
        {
            TransmitterClient webClient = new TransmitterClient();
            webClient.Open();
            webClient.Transmit(inXML, ref outXML);
            webClient.Close();
        }

        protected override void ParseResponse()
        {
            outXML = Contact.Crypto.VerefyMessage(outXML);
            Log.WriteLog(outXML, trava.Transact);
            ChekFault(outXML);
            GetState(outXML);
        }

        void GetState(string outXML)
        {
            XElement xDoc = XElement.Parse(outXML);
            string state = xDoc.Attribute("STATE").Value;
            trava.Fields.Add("state", GetStateTransfer(state));
        }
    }
}
