using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Bank
{
    public class BankServer
    {
        private static ServiceHost serviceHost;
        private static string address;

        public BankServer()
        {
            //Start();
        }

        public void Start(string bankAddress)
        {
            address = bankAddress;
            Task t = new Task(() =>
            {
                var binding = new NetTcpBinding();
                var endpoint = $"net.tcp://localhost:{bankAddress.Split(':')[1]}/Bank";
                serviceHost = new ServiceHost(typeof(Bank));
                serviceHost.AddServiceEndpoint(typeof(IBank), binding, endpoint);
                serviceHost.Open();
            });
            t.Start();
        }

        public void Stop()
        {
            serviceHost.Close();
        }
    }
}
