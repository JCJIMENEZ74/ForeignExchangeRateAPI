using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace apiExchangeRateGW.Models
{
    public class apiGatewayModel
    {
        public class ParamDtl
        {
            public string parameterCode { get; set; }
            public string gatewayId { get; set; }
            public string channelId { get; set; }
            public string portId { get; set; }
            public string machineId { get; set; }
        }

        public class TokenDtl
        {
            public string channelId { get; set; }
            public string tokenId { get; set; }
            public string allowedIP { get; set; }
        }

        public class LogDTL
        {
            public int reqId { get; set; }
            public string reqType { get; set; }
            public string channel { get; set; }
            public string logType { get; set; }
            public string code { get; set; }
            public string description { get; set; }
            public string details { get; set; }
            public string datetime { get; set; }
        }

        public class ReqForm
        {
            public string param { get; set; }
        }

        public class APIUser
        {
            public string username { get; set; }
            public string password { get; set; }
            public string salt { get; set; }
            public int status { get; set; }
        }

        public class APIUserDB
        {
            public string username { get; set; }
            public string password { get; set; }
            public string salt { get; set; }
            public int status { get; set; }
        }



        public class ExchangeRateRequest
        {
            public string channelId { get; set; }
            public string channelType { get; set; }
            public string visitorId { get; set; }
            public string fromCrncyCode { get; set; }
            public string toCrncyCode { get; set; }
            public string rateCode { get; set; }
            public int reqId { get; set; }
            public string AESencrypted { get; set; }
        }

    }
}