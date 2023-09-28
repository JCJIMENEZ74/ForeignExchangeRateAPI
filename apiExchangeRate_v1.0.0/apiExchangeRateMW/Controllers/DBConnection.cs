using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Configuration;
using System.Data.SqlClient;

namespace apiExchangeRateMW.Controllers
{
    public class DBConnection
    {
        public static SqlConnection GetGatewaySQLConnection()
        {
            //create the connection object
            return new SqlConnection(ConfigurationManager.ConnectionStrings["GWCONN"].ConnectionString);
        }

        public static SqlConnection GetMiddlewareSQLConnection()
        {
            //create the connection object
            return new SqlConnection(ConfigurationManager.ConnectionStrings["MWCONN"].ConnectionString);
        }
    }
}