using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace apiExchangeRateMW.Models
{
    public class apiMiddlewareModel
    {
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

        public class ExchangeRateRequest
        {
            public string channelId { get; set; }
            public string channelType { get; set; }
            public string visitorId { get; set; }
            public string fromCrncyCode { get; set; }
            public string toCrncyCode { get; set; }           
            public string rateCode { get; set; }
            public int reqId { get; set; }
        }



        public class ErrorDetail
        {
            public string errorCode { get; set; }
            public string errorDesc { get; set; }
            public string errorSource { get; set; }
            public string errorType { get; set; }
        }

    }
}