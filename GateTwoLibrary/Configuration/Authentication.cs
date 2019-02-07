using System;
using System.Collections.Generic;

namespace Gate
{
    public class Authentication
    {
        public Config GetAuthData()
        {
            try
            {
                return new Config();
            }
            catch (Exception)
            {
                throw new Exception($"Errors.ERROR_READ_AUTH_DATA, GATE: identificPoint");
            }
        }
    }
}
