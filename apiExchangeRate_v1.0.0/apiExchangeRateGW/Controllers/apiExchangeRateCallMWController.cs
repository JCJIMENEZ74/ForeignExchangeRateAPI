using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using System.Configuration;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Http.Description;
using System.Web.Mvc;

using static apiExchangeRateGW.Models.apiGatewayModel;

namespace apiExchangeRateGW.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class apiExchangeRateCallMWController : ApiController
    {
        public static string MWExchangeRateRequest(ExchangeRateRequest obj)
        {
            string responseStr = "";
            string MiddlewareURL = ConfigurationManager.AppSettings["MW_URL"];
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(MiddlewareURL);

            //var accountId = obj.accountId;

            var requestJSON = "{"
                 + "\"channelId\": \"" + obj.channelId + "\","
                 + "\"channelType\": \"" + obj.channelType + "\","
                 + "\"visitorId\": \"" + obj.visitorId + "\","
                 + "\"fromCrncyCode\": \"" + obj.fromCrncyCode + "\","
                 + "\"toCrncyCode\": \"" + obj.toCrncyCode + "\","               
                 + "\"rateCode\": \"" + obj.rateCode + "\","
                 + "\"reqId\": \"" + obj.reqId + "\""                
                 + "}";

            byte[] bytes;
            bytes = Encoding.ASCII.GetBytes(requestJSON);
            request.ContentType = "text/json; encoding='utf-8'";
            request.ContentLength = bytes.Length;
            request.Method = "POST";

            try
            {
                Stream postData = request.GetRequestStream();
                byte[] toBytes = UTF8Encoding.UTF8.GetBytes(requestJSON);
                postData.Write(toBytes, 0, toBytes.Length);
                postData.Close();
            }
            catch
            {
                throw;
            }

            HttpWebResponse response;
            response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                //Get ACTUAL stream response
                Stream responseStream = response.GetResponseStream();
                responseStr = new StreamReader(responseStream).ReadToEnd();

                return responseStr;
            }
            else
            {
                return responseStr;
            }


        }
    }
}
