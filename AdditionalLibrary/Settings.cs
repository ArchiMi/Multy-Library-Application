using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.IO;

namespace Helper
{
    public class Settings1
    {
        private String password = "123123";
        private String path = "DB.cfg";
        private XDocument document;

        public Settings1()
        {            
            String result = "";
            if (!File.Exists(path))
            {
                String documentText = $"<Connect database='processing_test' host='92.63.110.165' port='3306' login='gate_test' password='jcysdc71yqDcHc7cUW2q'/>";
                result = Encrypt(documentText, password);
                using (StreamWriter file = new StreamWriter(path))
                {
                    file.WriteLine(result);
                }
            }

            using (StreamReader file = new StreamReader(path))
            {
                result = file.ReadLine();
                String documentText = Decrypt(result, password);
                try
                {
                    document = XDocument.Parse(documentText);
                }
                catch
                {
                    MessageBox.Show($"При загрузке настроек из {path} возникла ошибка. \n Возможно в названии XML элемента использованы запрещенные символы (цифры/знаки)");
                }
            }
        }

        private static byte[] Encrypt(byte[] data, string password)
        {
            SymmetricAlgorithm sa = Rijndael.Create();
            ICryptoTransform ct = sa.CreateEncryptor(
                (new PasswordDeriveBytes(password, null)).GetBytes(16),
                new Byte[16]);

            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, ct, CryptoStreamMode.Write);

            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();

            return ms.ToArray();
        }

        static public string Encrypt(string data, string password)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(data), password));
        }

        static public string Decrypt(string data, string password)
        {
            CryptoStream cs = InternalDecrypt(Convert.FromBase64String(data), password);
            StreamReader sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        static CryptoStream InternalDecrypt(byte[] data, string password)
        {
            if (password != null)
            {
                SymmetricAlgorithm sa = Rijndael.Create();
                ICryptoTransform ct = sa.CreateDecryptor(
                    (new PasswordDeriveBytes(password, null)).GetBytes(16),
                    new byte[16]);

                MemoryStream ms = new MemoryStream(data);
                return new CryptoStream(ms, ct, CryptoStreamMode.Read);
            }
            else
                throw new Exception("Password is NULL.");
        }

        public void SaveXML()
        {
            String documentText = document.ToString();
            String result = Encrypt(documentText, password);
            using (StreamWriter file = new StreamWriter(path))
            {
                file.WriteLine(result);
            }
        }

        public string GetConnectParam(string attributeName)
        {
            return document.Element("Connect").Attribute(attributeName).Value;
        }

        public void SetConnectParam(string attributeName, string value)
        {
            try
            {
                document.Element("Connect").Attribute(attributeName).Value = value;

                SaveXML();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
