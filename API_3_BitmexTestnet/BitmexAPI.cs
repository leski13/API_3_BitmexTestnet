using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace API_3_BitmexTestnet
{
    public class OrderBookItem
    {
        public string Symbol { get; set; }
        public int Level { get; set; }
        public int BidSize { get; set; }
        public decimal BidPrice { get; set; }
        public int AskSize { get; set; }
        public decimal AskPrice { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    public class BitmexAPI
    {
        private const string domain = "https://testnet.bitmex.com";
        private string apiKey;
        private string apiSecret;
        private int rateLimit;

        public BitmexAPI(string bitmexKey="", string bitmexSecret="", int rateLimit = 5000)
        {
            this.apiKey = bitmexKey;
            this.apiSecret = bitmexSecret;
            this.rateLimit = rateLimit;
        }

        private string BuildQueryData(Dictionary<string, string> param)
        {
            if (param == null)
                return "";
            StringBuilder builder = new StringBuilder();
            foreach (var s in param)
                builder.Append(string.Format($"&{s.Key}={WebUtility.UrlEncode(s.Value)}"));

            try
            {
                return builder.ToString().Substring(1);
            }
            catch(Exception)
            {
                return "";
            }
        }

        private string BuildJSON(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            var entries = new List<string>();

            foreach (var s in param)
                entries.Add(string.Format($"\"{s.Key}\":\"{s.Value}\""));
            return "{" + string.Join(",", entries) + "}";
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (var s in ba)
                hex.AppendFormat($"{s:x2}");
            return hex.ToString();
        }

        private long GetExpires() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;

        private string Query(string method, string function, Dictionary<string, string> param=null, bool auth=false, bool json = false)
        {
            string paramData = json ? BuildJSON(param) : BuildQueryData(param);
            //string url = "/api/v1" + function + ((method == "GET" && paramData != "") ? "?" + paramData : "");
            string postData = (method != "GET") ? paramData : "";
            string url = "/api/v1/user/walletSummary";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(domain + url);
            request.Method = method;

            if (auth)
            {
                string expires = GetExpires().ToString();
                string message = method + url + expires + postData;
                byte[] signatureBytes = HMACSHA256(Encoding.UTF8.GetBytes(apiSecret), Encoding.UTF8.GetBytes(message));
                string signatureString = ByteArrayToString(signatureBytes);

                request.Headers.Add("api-expires", expires);
                request.Headers.Add("api-key", apiKey);
                request.Headers.Add("api-signature", signatureString);
            }

            try
            {
                if (postData != "")
                {
                    request.ContentType = json ? "application/json" : "application/x-www-form-urlencoded";
                    var data = Encoding.UTF8.GetBytes(postData);
                    using(var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }

                using (WebResponse response = request.GetResponse())
                using (Stream str = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(str))
                {
                    return reader.ReadToEnd();
                }
            }
            catch(WebException w)
            {
                using(HttpWebResponse response = (HttpWebResponse)w.Response)
                {
                    if (response == null)
                        throw;

                    using(Stream str = response.GetResponseStream())
                    {
                        using(StreamReader sr = new StreamReader(str))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }

        public string GetOrders()
        {
            var param = new Dictionary<string, string>();
            param["symbol"] = "XBTUSD";
            return Query("GET", "/order", param, true);
        }

        public string PostOrders()
        {
            var param = new Dictionary<string, string>();
            param["symbol"] = "XBTUSD";
            param["side"] = "Buy";
            param["orderQty"] = "1";
            param["ordType"] = "Market";
            return Query("POST", "/order", param, true);
        }

        public string DeleteOrders()
        {
            var param = new Dictionary<string, string>();
            param["orderID"] = "de709f12-2f24-9a36-b047-ab0ff090f0bb";
            param["text"] = "cancel order by ID";
            return Query("DELETE", "/order", param, true, true);
        }

        private byte[] HMACSHA256(byte[] v1, byte[] v2)
        {
            using(var hash = new HMACSHA256(v1))
            {
                return hash.ComputeHash(v2);
            }
        }

        private long lastTicks = 0;
        private object thisLock = new object();

        private void RateLimit()
        {
            lock (thisLock)
            {
                long elapsedTicks = DateTime.Now.Ticks - lastTicks;
                var timeSpan = new TimeSpan(elapsedTicks);
                if (timeSpan.TotalMilliseconds < rateLimit)
                    Thread.Sleep(rateLimit - (int)timeSpan.TotalMilliseconds);
                lastTicks = DateTime.Now.Ticks;
            }
        }
    }
}
