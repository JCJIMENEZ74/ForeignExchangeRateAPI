using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using static apiExchangeRateGW.Models.apiGatewayModel;

namespace apiExchangeRateGW.Controllers
{
    public class apiFuncDAL
    {
        private static string channelId = ConfigurationManager.AppSettings["ChannelId"];

        public static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) || //For object
                (strInput.StartsWith("[") && strInput.EndsWith("]"))) //For array
            {
                try
                {
                    var obj = JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    //Exception in parsing json
                    Console.WriteLine(jex.Message);
                    return false;
                }
                catch (Exception ex) //some other exception
                {
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static bool IsBase64String(string s)
        {
            s = s.Trim();
            return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,2}$", RegexOptions.None);
        }

        //From JWT spec
        private static byte[] Base64UrlDecode(string input)
        {
            var output = input;
            output = output.Replace('-', '+'); // 62nd char of encoding
            output = output.Replace('_', '/'); // 63rd char of encoding
            switch (output.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 1: output += "==="; break; // Three pad chars
                case 2: output += "=="; break; // Two pad chars
                case 3: output += "="; break; // One pad char
                default: throw new System.Exception("Illegal base64url string!");
            }
            var converted = Convert.FromBase64String(output); // Standard base64 decoder
            return converted;
        }

        private static byte[] FromBase64Url(string base64Url)
        {
            string padded = base64Url.Length % 4 == 0
                ? base64Url : base64Url + "====".Substring(base64Url.Length % 4);
            string base64 = padded.Replace("_", "/")
                                    .Replace("-", "+");
            return Convert.FromBase64String(base64);
        }

        public static string DecodePayload(string token, string pubkey, string cred1)
        {
            try
            {
                var RBpublicKey = GetRefVal(pubkey);
                var key = (RBpublicKey);

                StringReader sr = new StringReader(key);
                AsymmetricKeyParameter pr = (AsymmetricKeyParameter)new PemReader(sr).ReadObject();
                RSAParameters rsaParam = DotNetUtilities.ToRSAParameters((RsaKeyParameters)pr);
                RSACryptoServiceProvider csp = new RSACryptoServiceProvider();

                string[] parts = token.Split('.');
                string header = parts[0];
                string payload = parts[1];
                string headerJson = Encoding.UTF8.GetString(Base64UrlDecode(header));
                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payload));

                return payloadJson;
            }
            catch (Exception e)
            {

                return "Cannot decode param: " + e.ToString();
            }
        }

        //Get Ref Value from DB
        public static string GetRefVal(string code, string username = "", int type = 0)
        {
            try
            {
                string result = "";

                using (var conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    conn.Open();
                    //Create the SQLCommand object

                    using (SqlCommand command = new SqlCommand("spGetRefVal", conn) { CommandType = CommandType.StoredProcedure })
                    {
                        //Pass the parameter values here
                        command.Parameters.AddWithValue("@Code", code);
                        command.Parameters.AddWithValue("@Username", username);
                        command.Parameters.AddWithValue("@Type", type);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            //read the data
                            if (reader.Read())
                            {
                                result = Regex.Unescape(reader["Value"].ToString());
                            }

                            return result;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //write to logs
                LogDTL logs = new LogDTL();
                logs.reqId = 0;
                logs.code = "002";
                logs.description = "GetRefVal(string code, string username, int type = 0)";
                logs.details = e.ToString();
                AddLogsInfo(logs);

                return null;
            }
        }

        //Method to encrypt a string data and save it in a specific file using an AES algorithm  
        public static string AESEncrypt(string text)
        {
            // Create a new instance of the AES algorithm   
            SymmetricAlgorithm aes = new AesManaged();
            aes.BlockSize = 128;
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Convert.FromBase64String(GetRefVal("AESkey"));
            aes.IV = Convert.FromBase64String(GetRefVal("AESiv"));

            string encrypted = "";

            // Create an encryptor from the AES algorithm instance and pass the aes algorithm key and inialiaztion vector to generate a new random sequence each time for the same text  
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            // Create a memory stream to save the encrypted data in it  
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter writer = new StreamWriter(cs))
                    {
                        // Write the text in the stream writer   
                        writer.Write(text);
                    }
                }

                // Get the result as a byte array from the memory stream   
                byte[] encryptedDataBuffer = ms.ToArray();

                encrypted = Convert.ToBase64String(encryptedDataBuffer);
            }

            return encrypted;
        }

        //Method to decrypt a data from a specific file and return the result as a string   
        public static string AESDecrypt(string encrypted)
        {
            try
            {
                SymmetricAlgorithm aes = new AesManaged();
                aes.BlockSize = 128;
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Convert.FromBase64String(GetRefVal("AESkey"));
                aes.IV = Convert.FromBase64String(GetRefVal("AESiv"));

                string decrypted = "";

                // Create a decryptor from the aes algorithm   
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                // Read the encrypted bytes from the file   
                byte[] encryptedDataBuffer = Convert.FromBase64String(encrypted);

                // Create a memorystream to write the decrypted data in it   
                using (MemoryStream ms = new MemoryStream(encryptedDataBuffer))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cs))
                        {
                            // Reutrn all the data from the streamreader   
                            decrypted = reader.ReadToEnd();
                        }
                    }
                }

                return decrypted;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string JsonResp(string response = "{\"Code\": \"\",\"Message\": \"Invalid Request\"}")
        {
            string json = response;
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
            HttpContext.Current.Response.Write(json);
            HttpContext.Current.Response.End();

            return response;
        }

        #region Check connection availability (Database Gw & Mw, API Mw, Finacle, Bancnet)
        public static bool IsServerConnected()
        {
            bool result = false;
            try
            {
                using (var gw_conn = DBConnection.GetGatewaySQLConnection())
                {
                    gw_conn.Open();
                    result = true;
                }

                using (var mw_conn = DBConnection.GetMiddlewareSQLConnection())
                {
                    mw_conn.Open();
                    result = true;
                }

                return result;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static bool IsMWConnected(string MWURL)
        {
            try
            {
                MWURL = MWURL + "/Status";
                WebClient client = new WebClient();
                string rslt = client.DownloadString(MWURL);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        #endregion

        public static int GetRequestId(string refcode, string reftype)
        {
            try
            {
                int result;

                using (var conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    conn.Open();

                    //Create the SQLCommand object
                    using (SqlCommand command = new SqlCommand("spGenerateReqID", conn) { CommandType = System.Data.CommandType.StoredProcedure })
                    {
                        command.Parameters.AddWithValue("@ExtID", refcode);
                        command.Parameters.AddWithValue("@ReqType", reftype);
                        result = Convert.ToInt32(command.ExecuteScalar());
                        return result;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static string GetIP(int reqId)
        {
            string ipAddress = "";
            try
            {
                string getIP = ConfigurationManager.AppSettings["GetIP"];
                if (getIP == "Y")
                {
                    //var userIpAddress = HttpContext.Current.Request.UserHostAddress;
                    string strClientHostName = Dns.GetHostName();
                    ipAddress = Dns.GetHostAddresses(strClientHostName).GetValue(1).ToString();

                    if (Dns.GetHostEntry(IPAddress.Parse(HttpContext.Current.Request.UserHostAddress)).AddressList.Length > 1)
                        ipAddress = Dns.GetHostEntry(IPAddress.Parse(HttpContext.Current.Request.UserHostAddress)).AddressList[1].ToString();
                    else
                        ipAddress = Dns.GetHostEntry(IPAddress.Parse(HttpContext.Current.Request.UserHostAddress)).AddressList[0].ToString();
                }

                return ipAddress;
            }
            catch (Exception e)
            {
                //write to logs
                LogDTL logs = new LogDTL();
                logs.reqId = reqId;
                logs.code = "002";
                logs.description = "GetIP(int reqId)";
                logs.details = e.ToString();
                AddLogsInfo(logs);
            }

            return ipAddress;
        }

        public static void AddLogsInfo(LogDTL logs)
        {
            try
            {
                using (var conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    conn.Open();
                    //Create the SQLCommand object

                    using (SqlCommand command = new SqlCommand("spInsertLogs", conn) { CommandType = System.Data.CommandType.StoredProcedure })
                    {
                        //Pass the parameter values here
                        command.Parameters.AddWithValue("@ReqID", logs.reqId);
                        command.Parameters.AddWithValue("@ReqType", logs.reqType);
                        command.Parameters.AddWithValue("@Channel", logs.channel);
                        command.Parameters.AddWithValue("@LogType", logs.logType);
                        command.Parameters.AddWithValue("@Code", logs.code);
                        command.Parameters.AddWithValue("@Description", logs.description);
                        command.Parameters.AddWithValue("@Details", logs.details);

                        var result = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                //throw; //uncomment to throw error to show, or comment ignore it.
                throw e;
            }
        }

        //Insert Gateway Log
        public static bool InsertGatewayLog(string reqMode, string ipAddress, string httpMethod, string reqMsg, int reqId, int vId, string errorId = "")
        {
            bool isInsert = false;
            try
            {
                //Create the connection object
                using (var conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    conn.Open();

                    //Create the SQLCommand object
                    using (SqlCommand command = new SqlCommand("spInsertGatewayLog", conn) { CommandType = CommandType.StoredProcedure })
                    {
                        //Pass the parameter values here
                        command.Parameters.AddWithValue("@RequestMode", reqMode);
                        //command.Parameters.AddWithValue("@PortId", obj.PortId);
                        //command.Parameters.AddWithValue("@MachineId", obj.MachineId);
                        //command.Parameters.AddWithValue("@GatewayId", obj.GatewayId);
                        command.Parameters.AddWithValue("@ChannelId", channelId);
                        command.Parameters.AddWithValue("@RefId", reqId);
                        //command.Parameters.AddWithValue("@MessageCode", msgCode);
                        //command.Parameters.AddWithValue("@IntitutionCode", bankcode);
                        command.Parameters.AddWithValue("@VisitorId", vId);
                        command.Parameters.AddWithValue("@HttpMethod", httpMethod);
                        command.Parameters.AddWithValue("@ActualMessage", reqMsg);
                        command.Parameters.AddWithValue("@IP", ipAddress);
                        command.Parameters.AddWithValue("@ErrorLogId", errorId);
                        //command.ExecuteNonQuery();
                        int result = command.ExecuteNonQuery();

                        //Check success
                        if (result != 0)
                        {
                            isInsert = true;
                            return isInsert;
                        }
                    }
                }
                return isInsert;
            }
            catch (Exception e)
            {
                //write to logs
                LogDTL logs = new LogDTL();
                logs.reqId = reqId;
                logs.code = "002";
                logs.description = "InsertGatewayLog(string reqMode, string ipAddress, string httpMethod, string reqMsg, int reqId, int vId, string errorId)";
                logs.details = e.ToString();
                AddLogsInfo(logs);

                isInsert = false;
                return isInsert;
            }
        }

        public static bool ValidParameter(string paramcode, string tokenid, string channelid, string ipaddress)
        {
            try
            {
                if (apiFuncDAL.GetGatewayParam(paramcode) != null)
                {
                    if (apiFuncDAL.GetTokenParam(channelid, tokenid, ipaddress) != null)
                    {
                        return true;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }
            catch (Exception e)
            {
                return false;
                throw e;
            }
        }

        public static ParamDtl GetGatewayParam(string paramCode)
        {
            ParamDtl obj = new ParamDtl();
            obj = null;

            try
            {
                //Create the connection object
                using (var gw_conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    gw_conn.Open();
                    //Create the SQLCommand object
                    using (SqlCommand command = new SqlCommand("spGetGatewayParam", gw_conn) { CommandType = CommandType.StoredProcedure })
                    {
                        //Pass the parameter values here
                        command.Parameters.AddWithValue("@ParamCode", paramCode);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            //read the data
                            if (reader.Read())
                            {
                                obj = new ParamDtl();
                                obj.parameterCode = reader["ParameterCode"].ToString();
                                obj.gatewayId = reader["GatewayID"].ToString();
                                obj.channelId = reader["ChannelID"].ToString();
                                obj.portId = reader["PortID"].ToString();
                                obj.machineId = reader["MachineID"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return null;
                throw e;
            }
            return obj;
        }

        public static TokenDtl GetTokenParam(string channelId, string tokenId, string ipAddress)
        {
            TokenDtl obj = new TokenDtl();
            obj = null;

            //Create the connection object
            using (var gw_conn = DBConnection.GetGatewaySQLConnection())
            {
                // Open the SqlConnection.
                gw_conn.Open();
                //Create the SQLCommand object

                using (SqlCommand command = new SqlCommand("spGetTokenParam", gw_conn) { CommandType = CommandType.StoredProcedure })
                {
                    //Pass the parameter values here
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@TokenId", tokenId);
                    command.Parameters.AddWithValue("@IPAddress", ipAddress);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        //read the data
                        if (reader.Read())
                        {
                            obj = new TokenDtl();
                            obj.channelId = reader["ChannelId"].ToString();
                            obj.tokenId = reader["TokenID"].ToString();
                            obj.allowedIP = reader["AllowedIP"].ToString();
                        }
                    }
                }
            }

            return obj;
        }

        public static APIUserDB UserCred(string username)
        {
            APIUserDB obj = new APIUserDB();

            obj = null;

            try
            {
                //Create the gw_connection object
                using (var gw_conn = DBConnection.GetGatewaySQLConnection())
                {
                    // Open the SqlConnection.
                    gw_conn.Open();
                    //Create the SQLCommand object
                    using (SqlCommand command = new SqlCommand("spGetCred", gw_conn) { CommandType = CommandType.StoredProcedure })
                    {
                        //Pass the parameter values here
                        command.Parameters.AddWithValue("@Username", username);
                        using (SqlDataReader reader = command.ExecuteReader())

                        {

                            //read the data
                            if (reader.Read())
                            {
                                obj = new APIUserDB();

                                obj.username = reader["Username"].ToString();
                                obj.salt = reader["Salt"].ToString();
                                obj.password = reader["Password"].ToString();
                            }
                        }
                    }
                }
                return obj;
            }
            catch (Exception e)
            {
                return obj;
                throw e;
            }
        }

        public static bool IsNumeric(string pAmount)
        {            
            double retNum;
            bool IsNum = false;
            if (String.IsNullOrEmpty(pAmount))
            {
                IsNum = false;
            }
            else
            {
                IsNum = Double.TryParse(pAmount, System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
            }

            return IsNum;
        }




    }
}