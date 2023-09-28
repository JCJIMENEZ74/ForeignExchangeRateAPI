using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;

using System.Net;
using System.Net.Security;
using System.Text;

using static apiExchangeRateMW.Models.apiMiddlewareModel;

namespace apiExchangeRateMW.Controllers
{
    public class apiFuncDAL
    {
        private static string finBankId = (ConfigurationManager.AppSettings["FinBankId"] != null ? ConfigurationManager.AppSettings["FinBankId"] : "01"); //incase null then 01

        public static void AddLogsInfo(LogDTL logs)
        {
            try
            {
                using (var conn = DBConnection.GetMiddlewareSQLConnection())
                {
                    // Open the SqlConnection.
                    conn.Open();

                    //Create the SQLCommand object
                    using (SqlCommand command = new SqlCommand("spInsertLogsInfo", conn) { CommandType = CommandType.StoredProcedure })
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
                throw e;
            }
        }


        public string ExchangeRateInq(string url, ExchangeRateRequest obj )
        {
            var reqType = "ExchangeRateInq";

            LogDTL logs = new LogDTL();
            logs.reqId = obj.reqId;
            logs.reqType = reqType;
            logs.channel = obj.channelId;

            #region requestXML
            string requestXml = @"<?xml version='1.0' encoding='UTF-8'?>"
                                    + "<FIXML xsi:schemaLocation=\"http://www.finacle.com/fixml getExchangeRateForRateCode.xsd\""
                                        + " xmlns=\"http://www.finacle.com/fixml\""
                                        + " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                        + "<Header>"
                                            + "<RequestHeader>"
                                                + "<MessageKey>"
                                                    + "<RequestUUID>Req_" + DateTime.Now.ToString("MMddyyHHmmssf") + "</RequestUUID>"
                                                    + "<ServiceRequestId>getExchangeRateForRateCode</ServiceRequestId>"
                                                    + "<ServiceRequestVersion>10.2</ServiceRequestVersion>"
                                                    + "<ChannelId>COR</ChannelId>"
                                                + "</MessageKey>"
                                                + "<RequestMessageInfo>"
                                                    + "<BankId></BankId>"
                                                    + "<TimeZone>GMT+05:00</TimeZone>"
                                                    + "<EntityId></EntityId>"
                                                    + "<EntityType></EntityType>"
                                                    + "<ArmCorrelationId></ArmCorrelationId>"
                                                    + "<MessageDateTime>" + DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fff") + "</MessageDateTime>"
                                                + "</RequestMessageInfo>"
                                                + "<Security>"
                                                    + "<Token>"
                                                        + "<PasswordToken>"
                                                            + "<UserId></UserId>"
                                                            + "<Password></Password>"
                                                        + "</PasswordToken>"
                                                    + "</Token>"
                                                    + "<FICertToken></FICertToken>"
                                                    + "<RealUserLoginSessionId></RealUserLoginSessionId>"
                                                    + "<RealUser></RealUser>"
                                                    + "<RealUserPwd></RealUserPwd>"
                                                    + "<SSOTransferToken></SSOTransferToken>"
                                                + "</Security>"
                                            + "</RequestHeader>"
                                        + "</Header>"
                                        + "<Body>"
                                            + "<getExchangeRateForRateCodeRequest>"
                                                  + "<ExchangeRateForRateCodeInputVO>"
                                                        + "<fromCrncyCode>" + obj.fromCrncyCode + "</fromCrncyCode>"
                                                        + "<rateCode>" + obj.rateCode + "</rateCode>"
                                                        + "<toCrncyCode>" + obj.toCrncyCode + "</toCrncyCode>"
                                                  + "</ExchangeRateForRateCodeInputVO>"
                                                  + "<getExchangeRateForRateCode_CustomData></getExchangeRateForRateCode_CustomData>"
                                            + "</getExchangeRateForRateCodeRequest>"
                                        + "</Body>"
                                    + "</FIXML>";

            #endregion

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            byte[] bytes;
            bytes = UTF8Encoding.UTF8.GetBytes(requestXml);
            request.ContentType = "text/xml; encoding='utf-8'";
            request.ContentLength = bytes.Length;
            request.Method = "POST";

            logs.logType = "INFO";
            logs.code = "010";
            logs.description = "Actual Request";
            logs.details = "Payload: [" + requestXml + "]";
            AddLogsInfo(logs);

            try
            {
                if (ConfigurationManager.AppSettings["BypassCert"] == "Y")
                    ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });

                Stream postData = request.GetRequestStream();
                byte[] toBytes = UTF8Encoding.UTF8.GetBytes(requestXml);

                postData.Write(toBytes, 0, toBytes.Length);
                postData.Close();
            }
            catch (Exception e)
            {
                logs.logType = "FATAL";
                logs.code = "008";
                logs.description = "Fatal Error";
                logs.details = "Error Trace: [" + e.ToString() + "]";
                AddLogsInfo(logs);

                return "Fatal Error";
            }

            Stream requestStream = request.GetRequestStream();
            requestStream.Close();
            HttpWebResponse response;

            response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream responseStream = response.GetResponseStream();
                string responseStr = new StreamReader(responseStream).ReadToEnd();

                logs.logType = "INFO";
                logs.code = "011";
                logs.description = "Actual Response";
                logs.details = "Response: [" + responseStr + "]";
                AddLogsInfo(logs);

                return responseStr;
            }
            else
            {
                logs.logType = "ERROR";
                logs.code = "007";
                logs.description = "Error";
                logs.details = "Response: [" + response.StatusCode + "]";
                AddLogsInfo(logs);

                return "Bad Request";
            }


        }




    }
}