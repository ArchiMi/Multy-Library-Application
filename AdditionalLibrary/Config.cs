using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Additional
{
    public class Config
    {
        private static string hows;

        public static string Hows
        {
            get { return hows; }
            set { hows = value; }
        }

        public static string HelperSourceName
        {
            get { return "Additional Library"; }
        }

        public static Dictionary<string, string> JsonToDictionary(string fieldsStr)
        {
            Dictionary<string, string> fields = JsonConvert.DeserializeObject<Dictionary<string, string>>(fieldsStr);
            return fields;
        }
    }
}
