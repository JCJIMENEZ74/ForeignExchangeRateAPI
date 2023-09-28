using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using Newtonsoft.Json;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

using static apiExchangeRateMW.Models.apiMiddlewareModel;

namespace apiExchangeRateMW.Controllers
{

    public class apiExchangeRate_MWController : ApiController
    {
        string str = "";

        [HttpGet, Route("~/api/mw/v1/getExchangeRate/Status")]
        public string CheckMW()
        {
            return "Success";
        }

        [HttpPost, Route("~/api/mw/v1/getExchangeRate")]
        public async Task<string> GeneralAcccountInqPost()
        {
            str = await Request.Content.ReadAsStringAsync();

            var reqType = "ExchangeRate";

            LogDTL logs = new LogDTL();
            logs.reqType = reqType;

            try
            {
                ExchangeRateRequest obj = new ExchangeRateRequest();
                try
                {
                    string regStr = Regex.Unescape(str);
                    obj = JsonConvert.DeserializeObject<ExchangeRateRequest>(regStr);

                    var responseStr = "";
                    var statuscode = 0;

                    logs.reqId = obj.reqId;
                    logs.channel = obj.channelId;
                    logs.logType = "INFO";
                    logs.code = "001";
                    logs.description = "Middleware In";
                    logs.details = "Payload: [" + str + "]";
                    apiFuncDAL.AddLogsInfo(logs);

                    HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                    httpResponseMessage = ExchangeRateInq(obj);

                    var response = await httpResponseMessage.Content.ReadAsStringAsync();
                    var RegResp = Regex.Unescape(response).TrimStart('"').TrimEnd('"');

                    response = RegResp;

                    string FinStat = "";
                    using (var strReader = new StringReader(RegResp))
                    using (var xmlReader = XmlReader.Create(strReader))
                    {
                        XmlNodeType nType = xmlReader.NodeType;

                        while (xmlReader.Read() && FinStat == "")
                        {
                            if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "HostTransaction")
                            {
                                while (xmlReader.Read())
                                {
                                    if (xmlReader.NodeType == XmlNodeType.Element &&
                                        xmlReader.Name == "Status")
                                    {
                                        FinStat = xmlReader.ReadString();
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    XDocument xDoc = XDocument.Parse(Regex.Unescape(response).TrimStart('"').TrimEnd('"'));
                    XNamespace ns = xDoc.Root.Name.Namespace;
                    XElement elm = xDoc.Root.Element(ns + "Body");

                    string apiResp = "";

                    if (FinStat == "SUCCESS")
                    {
                        elm = xDoc.Root.Element(ns + "Body").Element(ns + "getExchangeRateForRateCodeResponse");
                    }
                    else if (FinStat == "FAILURE")
                    {
                        var errElm = xDoc.Root.Element(ns + "Body").Element(ns + "Error").Elements().FirstOrDefault().Name;
                        elm = xDoc.Root.Element(ns + "Body").Element(ns + "Error").Element(ns + errElm.LocalName).Element(ns + "ErrorDetail");
                    }
                    else
                    {
                        apiResp = "{\"Message\": \"Error API Resp\"}";
                    }

                    if (apiResp == "")
                    {
                        var doc = new XmlDocument();
                        doc.LoadXml(Regex.Unescape(elm.ToString().Replace(@"xmlns=""http://www.finacle.com/fixml""", "")).TrimStart('"').TrimEnd('"'));
                        var jsonText = JsonConvert.SerializeXmlNode(doc);
                        apiResp = jsonText;
                        statuscode = 200;
                    }

                    string jsonresp = apiResp;
                    HttpContext.Current.Response.Clear();
                    HttpContext.Current.Response.StatusCode = statuscode;
                    HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
                    HttpContext.Current.Response.Write(apiResp);
                    HttpContext.Current.Response.End();

                    logs.logType = "INFO";
                    logs.code = "012";
                    logs.description = "Middleware Out";
                    logs.details = "Response Sent: [" + apiResp + "]";
                    apiFuncDAL.AddLogsInfo(logs);

                    return response;


                }
                catch (Exception exc)
                {
                    logs.code = "010";
                    logs.description = "Middleware Error";
                    logs.details = "Middleware error [" + exc.ToString() + "]";
                    apiFuncDAL.AddLogsInfo(logs);

                    throw new Exception("Error. ", exc);
                }

            }
            catch (Exception ex)
            {
                logs.code = "011";
                logs.description = "Middleware Error";
                logs.details = "Middleware error [" + ex.ToString() + "]";
                apiFuncDAL.AddLogsInfo(logs);

                throw new Exception("Error. ", ex);
            }

        }


        public HttpResponseMessage ExchangeRateInq(ExchangeRateRequest obj)
        {
            var finacleURL = ConfigurationManager.AppSettings["FinacleURL"];
            var reqType = "ExchangeRateInq";

            //Initialize HTTP response message
            HttpResponseMessage Response = new HttpResponseMessage();

            //Middleware URL if on-us
            HttpWebRequest finacleRequest = (HttpWebRequest)WebRequest.Create(finacleURL);
            apiFuncDAL apiFunc = new apiFuncDAL();

            //Insert Middleware In Log
            LogDTL logs = new LogDTL();
            logs.reqId = obj.reqId;
            logs.reqType = reqType;
            logs.channel = obj.channelId;
            logs.logType = "INFO";
            logs.code = "005";
            logs.description = "Start of Request Processing";
            logs.details = "";
            apiFuncDAL.AddLogsInfo(logs);

            string finacleResp = apiFunc.ExchangeRateInq(finacleURL, obj).ToString();

            if (string.IsNullOrEmpty(finacleResp))
            {
                //Failed Transaction Sending to Middleware
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            //If no error, get ACTUAL reply from middleware
            else
            {
                return Request.CreateResponse(finacleResp);
            }
        }



    }
}
