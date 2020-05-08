using System;
using System.Linq;
using API_3_BitmexTestnet;
using Newtonsoft.Json;

namespace API_3_BitmexTestnet
{
    class Program
    {
        private static string bitmexKey = "";
        private static string bitmexSecret = "";

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        private void Run(string[] args)
        {
            BitmexAPI bitmex = new BitmexAPI(bitmexKey, bitmexSecret);
            var orders = bitmex.GetOrders();
            var s = JsonConvert.DeserializeObject(orders);
            Console.WriteLine(s);
        }
    }
}
