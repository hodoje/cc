using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Bookstore
{
    public class BookstoreServer
    {
        private static ServiceHost serviceHost;
        private static string address;

        public BookstoreServer()
        {
            //Start();
        }

        public void Start(string bookstoreAddress)
        {
            address = bookstoreAddress;
            Task t = new Task(() =>
            {
                var binding = new NetTcpBinding();
                var endpoint = $"net.tcp://localhost:{bookstoreAddress.Split(':')[1]}/Bookstore";
                serviceHost = new ServiceHost(typeof(Bookstore));
                serviceHost.AddServiceEndpoint(typeof(IBookstore), binding, endpoint);
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
