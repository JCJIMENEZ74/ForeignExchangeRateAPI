using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Web;
using Savage.Credentials;
using System.Runtime.Serialization.Json;
using System.Globalization;
using System.Configuration;
using System.Text.RegularExpressions;

using static apiExchangeRateGW.Models.apiGatewayModel;
using apiExchangeRateGW.Controllers;

namespace apiExchangeRateGW.Controllers
{
    public class apiExchangeRate_GWController : ApiController
    {
        public string str;
        public string authToken;
        public string authType;
        public string apiResp = "";
        public string MiddlewareURL = ConfigurationManager.AppSettings["MW_URL"];

        public int authTokenValid { get; private set; }

        [HttpGet, Route("~/api/v1/CheckConnections")]
        public string CheckConnections()
        {
            try
            {
                bool isServerConnected = apiFuncDAL.IsServerConnected();
                bool isMWConnected = apiFuncDAL.IsMWConnected(MiddlewareURL);

                if (!isServerConnected || !isMWConnected)
                    return "Failed";
                else
                    return "Success";
            }
            catch (Exception e)
            {
                return "Failed connection to server.";

                throw e;
            }
        }

        [HttpPost, Route("~/api/v1/getExchangeRate")]
        public async Task<string> GetExchangeRatePost()
        {
            str = await Request.Content.ReadAsStringAsync();
            int reqId;
            var refCode = "";
            var ipAddress = "";
            var reqType = "ExchangeRate";
            var CifRequest = String.Empty;
            var JResponse = String.Empty;
            var response = String.Empty;
            var errormessage = String.Empty;
            var statuscode = 0;
            var errorstat = 0; // Error Status, 0 means no error(s)
            var errorcode = "";
            var errortype = "";
            var edpResponse = "";
            string requestString = "";
            bool isInsertGatewayLog = false;
            string httpMethod = "";

            var paramCodeIn = ConfigurationManager.AppSettings["ParameterCodeIn"];
            var paramCodeOut = ConfigurationManager.AppSettings["ParameterCodeOut"];

            ExchangeRateRequest objRequest = new ExchangeRateRequest();
            ParamDtl ParamDtlObj = new ParamDtl();

            reqId = apiFuncDAL.GetRequestId(refCode, reqType);

            LogDTL logs = new LogDTL();
            logs.reqId = reqId;
            logs.reqType = reqType;

            try
            {
                if (CheckConnections() == "Success")
                {
                    ipAddress = apiFuncDAL.GetIP(reqId);
                    requestString = HttpContext.Current.Request.Url.OriginalString;
                    httpMethod = HttpContext.Current.Request.HttpMethod;
                    var authTokenValid = 0;

                    //Insert Gateway Log
                    logs.logType = "INFO";
                    logs.code = "001";
                    logs.description = "Gateway In";
                    logs.details = "Originating IP Address: [" + ipAddress + "], " + "Payload: [" + str + "]";
                    apiFuncDAL.AddLogsInfo(logs);

                    //Insert Gateway Log
                    isInsertGatewayLog = apiFuncDAL.InsertGatewayLog("IN", ipAddress, httpMethod, requestString + "-" + str, reqId, reqId);

                    //Check if header authorization was received 
                    if (Request.Headers.Authorization is null)
                    {
                        errorstat = 1;
                        errormessage = "No Authorization Header";
                        statuscode = 401;
                        errorcode = "1103";
                        errortype = "GW";

                        logs.logType = "ERROR";
                        logs.code = "002";
                        logs.description = "Authorization Error";
                        logs.details = "Error Code: [" + errorcode + "], " + "Error Message: [" + errormessage + "], " + "Error Type: [" + errortype + "]";
                        apiFuncDAL.AddLogsInfo(logs);
                    }
                    else if (Request.Headers.Authorization.Scheme is null)
                    {
                        errorstat = 1;
                        errormessage = "No Authorization Header Scheme";
                        statuscode = 401;
                        errorcode = "1104";
                        errortype = "GW";

                        logs.logType = "ERROR";
                        logs.code = "002";
                        logs.description = "Authorization Error";
                        logs.details = "Error Code: [" + errorcode + "], " + "Error Message: [" + errormessage + "], " + "Error Type: [" + errortype + "]";
                        apiFuncDAL.AddLogsInfo(logs);
                    }
                    else if (Request.Headers.Authorization.Parameter is null)
                    {
                        errorstat = 1;
                        errormessage = "No Authorization Token";
                        statuscode = 401;
                        errorcode = "1105";
                        errortype = "GW";

                        logs.logType = "ERROR";
                        logs.code = "002";
                        logs.description = "Authorization Error";
                        logs.details = "Error Code: [" + errorcode + "], " + "Error Message: [" + errormessage + "], " + "Error Type: [" + errortype + "]";
                        apiFuncDAL.AddLogsInfo(logs);
                    }
                    else
                    {
                        // Token checker -Start
                        bool authTokenb64 = apiFuncDAL.IsBase64String(Request.Headers.Authorization.Parameter);

                        if (authTokenb64 == false)
                        {
                            errorstat = 1;
                            errormessage = "Missing or Invalid Authorization Format";
                            statuscode = 400;
                            authTokenValid = 1; // 1 for invalid
                            errorcode = "1101";
                            errortype = "GW";

                            logs.logType = "ERROR";
                            logs.code = "002";
                            logs.description = "Authorization Error";
                            logs.details = "Error Code: [" + errorcode + "], " + "Error Message: [" + errormessage + "], " + "Error Type: [" + errortype + "]";
                            apiFuncDAL.AddLogsInfo(logs);
                        }

                        authToken = Request.Headers.Authorization.Parameter;
                        authType = Request.Headers.Authorization.Scheme;

                        TokenDtl objt = new TokenDtl();

                        if (apiFuncDAL.ValidParameter(paramCodeIn, objt.tokenId, objt.channelId, ipAddress) == false)
                        {
                            //bypass temporary
                            //HttpResponseMessage responseMessage = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                            //instaPayResponse = responseMessage.StatusCode.ToString();
                            //return instaPayResponse;

                            //instaPayResponse = "{\"Code\": \"\","
                            //                     + "\"Message\": \"Unauthorized\"}";

                            //string json = instaPayResponse;
                            //HttpContext.Current.Response.Clear();
                            //HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
                            //HttpContext.Current.Response.Write(json);
                            //HttpContext.Current.Response.End();

                            //return response;
                        }

                        ParamDtlObj = new ParamDtl();
                        ParamDtlObj = apiFuncDAL.GetGatewayParam(paramCodeIn);

                        if (ParamDtlObj == null)
                        {
                            edpResponse = "{\"Code\": \"\","
                                        + "\"Message\": \"Bad Gateway, channel parameter not define.\"}";

                            logs.logType = "ERROR";
                            logs.code = "011";
                            logs.description = "Bad Gateway";
                            logs.details = "Error in Gateway, channel parameter not define.";
                            apiFuncDAL.AddLogsInfo(logs);

                            return apiFuncDAL.JsonResp(edpResponse);
                        }
                        else
                        {
                            if (authToken != null && authType != null && !string.IsNullOrEmpty(authToken) && authTokenValid == 0 && authType.Equals("basic", StringComparison.OrdinalIgnoreCase))
                            {
                                var encoding = System.Text.Encoding.GetEncoding("iso-8859-1");
                                var credentialstring = encoding.GetString(Convert.FromBase64String(authToken));
                                var credential = credentialstring.Split(':');
                                var cred1 = credential[0];
                                var inputpw = credential[1];

                                APIUserDB apiuserdb = new APIUserDB();
                                apiuserdb = apiFuncDAL.UserCred(cred1);

                                //Credentials from DB
                                var salt1 = Convert.FromBase64String(apiuserdb.salt);
                                var hashPassword = Convert.FromBase64String(apiuserdb.password);

                                //Call the static load method of the SaltAndHashedPassword
                                var saltHashedPassword = SaltAndHashedPassword.Load(salt1, hashPassword);

                                //Check if user is authorized
                                bool authenticated = saltHashedPassword.ComparePassword(inputpw); // authenticated = true

                                ReqForm rObj = new ReqForm();
                                var tststr = str.Trim();

                                if (ConfigurationManager.AppSettings["JWTencrypt"] == "Y")
                                {
                                    string pubkey = ConfigurationManager.AppSettings["PubKey"];

                                    try
                                    {
                                        //Validate if json request
                                        tststr = str.Trim();
                                        if (!tststr.StartsWith("{") && !tststr.EndsWith("}"))
                                            return apiFuncDAL.JsonResp();
                                        else
                                            rObj = JsonConvert.DeserializeObject<ReqForm>(tststr);

                                        string decrptedstr = apiFuncDAL.DecodePayload(rObj.param, pubkey, cred1);
                                        if (decrptedstr == "" || decrptedstr.Contains("Cannot decode param:"))
                                            return apiFuncDAL.JsonResp();

                                        tststr = decrptedstr.Trim();
                                    }
                                    catch (Exception ex)
                                    {
                                        var err = ex.Message;
                                        logs.logType = "ERROR";
                                        logs.code = "011";
                                        logs.description = "Decryption Error";
                                        logs.details = ex.Message;
                                        apiFuncDAL.AddLogsInfo(logs);

                                        return apiFuncDAL.JsonResp();
                                    }

                                    //Return the string value to str
                                    str = tststr;
                                }

                                //Check if post body is valid JSON
                                bool isJsonValid = apiFuncDAL.IsValidJson(str);

                                if (isJsonValid == false)
                                {
                                    errorstat = 1;
                                    errormessage = "Invalid JSON or Request Body";
                                    statuscode = 400;
                                    errorcode = "1130";
                                    errortype = "GW";
                                }

                                if (authenticated == false)
                                {
                                    // return "Invalid Authorization";
                                    errorstat = 1;
                                    errormessage = "Invalid Token / Incorrect Username or Password";
                                    statuscode = 401;
                                    errorcode = "1102";
                                    errortype = "GW";
                                }

                               
                                if (authenticated && isJsonValid)
                                {
                                    logs.logType = "INFO";
                                    logs.code = "003";
                                    logs.description = "Authenticated Request";
                                    logs.details = "User: [" + cred1 + "]";
                                    apiFuncDAL.AddLogsInfo(logs);

                                    objRequest = JsonConvert.DeserializeObject<ExchangeRateRequest>(str);
                                    objRequest.reqId = reqId;
                                    var channelId = objRequest.channelId;

                                    var fromCrncyCode =  objRequest.fromCrncyCode;
                                    var toCrncyCode = objRequest.toCrncyCode;                                   
                                    var rateCode = objRequest.rateCode;


                                    logs.channel = channelId;

                                    if (String.IsNullOrEmpty(channelId) || String.IsNullOrEmpty(fromCrncyCode) || String.IsNullOrEmpty(toCrncyCode) || String.IsNullOrEmpty(rateCode))
                                    {
                                        errorstat = 1;
                                        errorcode = "1131";
                                        errormessage = "Please specify values for required fields";
                                        statuscode = 400;
                                        errortype = "GW";
                                    }
 
                                    if (errorstat == 0)
                                    {
                                        logs.logType = "INFO";
                                        logs.code = "005";
                                        logs.description = "Start of Request Processing";
                                        logs.details = "Payload: [" + str + "]";
                                        apiFuncDAL.AddLogsInfo(logs);

                                        string responseStr = apiExchangeRateCallMWController.MWExchangeRateRequest(objRequest);
                                        JResponse = responseStr;
                                        statuscode = 200;

                                        string jsonresp = JResponse;
                                        HttpContext.Current.Response.Clear();
                                        HttpContext.Current.Response.StatusCode = statuscode;
                                        HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
                                        HttpContext.Current.Response.Write(jsonresp);
                                        HttpContext.Current.Response.End();

                                        logs.logType = "INFO";
                                        logs.code = "006";
                                        logs.description = "Gateway Out";
                                        logs.details = "GW Response [" + jsonresp + "]";
                                        apiFuncDAL.AddLogsInfo(logs);

                                        response = jsonresp;   //jj

                                        return response;
                                    }
                                    else
                                    {
                                        // Error Messages
                                        JResponse = "{\"errorcode\":\"" + errorcode + "\","
                                                    + "\"errordesc\":\"" + errormessage + "\","
                                                    + "\"errortype\":\"" + errortype + "\""
                                                    + "}";

                                        string jsonerrorr2 = JResponse;
                                        HttpContext.Current.Response.Clear();
                                        HttpContext.Current.Response.StatusCode = statuscode;
                                        HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
                                        HttpContext.Current.Response.Write(jsonerrorr2);
                                        HttpContext.Current.Response.End();

                                        logs.logType = "INFO";
                                        logs.code = "006";
                                        logs.description = "Gateway Out";
                                        logs.details = "GW Response [" + jsonerrorr2 + "]";
                                        apiFuncDAL.AddLogsInfo(logs);

                                        return response;
                                    }
                                }
                            }
                        }

                        // Token checher -End

                    }


                }
                else
                {
                    apiResp = "{\"Code\": \"501\",\"Message\": \"Connections to DB or MW server may be down.\"}";
                    errormessage = "Connections to DB or MW server may be down.";
                    errorcode = "1199";
                    statuscode = 500;
                    errortype = "GW";
                }

            }
            catch (Exception ex)
            {
                if (ex is IndexOutOfRangeException)
                {
                    errormessage = "Missing or Invalid Authorization Format";
                    errorcode = "1101";
                    statuscode = 401;
                    errortype = "GW";
                }
                else
                {
                    // General Error
                    errormessage = "Something went wrong. Please try again later.";
                    errorcode = "1199";
                    statuscode = 500;
                    errortype = "GW";
                }
            }

            // Error Messages
            JResponse = "{\"errorcode\":\"" + errorcode + "\","
                        + "\"errordesc\":\"" + errormessage + "\","
                        + "\"errortype\":\"" + errortype + "\""
                        + "}";

            string jsonerrorr = JResponse;
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.StatusCode = statuscode;
            HttpContext.Current.Response.ContentType = "application/json; charset=utf-8";
            HttpContext.Current.Response.Write(jsonerrorr);
            HttpContext.Current.Response.End();

            return response;


        }





    }
}
