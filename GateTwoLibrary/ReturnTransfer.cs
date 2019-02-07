using Gate.ContactWebService;
using Helper;
using System.Text;
using System.Xml;

namespace Gate
{
    public class ReturnTransfer: Operation
    {
        string outXML;
        XmlDocument xDoc;
        string inXML;

        public ReturnTransfer(TransactionValues trava) : base(trava)
        {
            xDoc = new XmlDocument();
            trava.Fields["knp"] = trava.DocIDFromKnp;
        }

        protected override void CheckTransactionValues()
        {
            CheckTransactionValues(trava, OperationCommands.RETURN_TRANSFER);
        }

        protected override void PrepareRequest()
        {
            XmlDeclaration xmlDeclaration = xDoc.CreateXmlDeclaration("1.0", "utf-8", null);
            XmlElement xREQUEST = xDoc.CreateElement("REQUEST");
            xREQUEST = xDoc.CreateElement("REQUEST");
            xDoc.InsertBefore(xmlDeclaration, xDoc.DocumentElement);
            xDoc.AppendChild(xREQUEST);
            AddAttribute(xDoc, xREQUEST, "OBJECT_CLASS", ActionsContact.REQUEST_OBJECT);
            AddAttribute(xDoc, xREQUEST, "ACTION", ActionsContact.RETURN_TRANSFER);
            AddAttribute(xDoc, xREQUEST, "DOC_ID", trava.Fields["knp"]);
            AddAttribute(xDoc, xREQUEST, "INT_SOFT_ID", Config.Guid);
            AddAttribute(xDoc, xREQUEST, "POINT_CODE", Config.PointCode);
            AddAttribute(xDoc, xREQUEST, "USER_ID", "1");
            AddAttribute(xDoc, xREQUEST, "LANG", "RU");
            Encoding win1251 = Encoding.GetEncoding("windows-1251");
            byte[] inXML_win1251_buffer = Encoding.Convert(Encoding.UTF8, win1251, Encoding.UTF8.GetBytes(xDoc.InnerXml));
            inXML = Contact.Crypto.CreateAndSignMessage(inXML_win1251_buffer, Encoding.Default.GetBytes(Config.PointCode), false, true);
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
            ChekFault(outXML);
        }
    }
}
