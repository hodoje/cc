﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Contract;

namespace ConsoleApp
{
    public class ContainerServer
    {
        private ServiceHost _serviceHost;

        public ContainerServer()
        {
        }

        public void Start(int port)
        {
            _serviceHost = new ServiceHost(typeof(Container));
            var binding = new NetTcpBinding();
            // This won't allow the connection to timeout
            binding.ReliableSession.Enabled = true;
            // The first inactivity timer is on the reliable session and is called the InactivityTimeout.
            // This inactivity timer fires if no messages, either application or infrastructure, are received within the timeout period.
            // An infrastructure message is a message that is generated for the purpose of one of the protocols in the channel stack, 
            // such as a keep alive or an acknowledgment, rather than containing application data.
            binding.ReliableSession.InactivityTimeout = TimeSpan.FromHours(1);
            // The second inactivity timer is on the service and uses the ReceiveTimeout setting of the binding.
            // This inactivity timer fires if no application messages are received within the timeout period. 
            // This specifies, for example, the maximum time a client may take to send at least one message to the server before the server 
            // will close the channel used by a session.
            // This behavior ensures that clients cannot hold on to server resources for arbitrary long periods.
            binding.ReceiveTimeout = TimeSpan.FromHours(1);
            binding.ReliableSession.Ordered = false;
            _serviceHost.AddServiceEndpoint(
                typeof(IContainer), 
                binding,
                new Uri($"net.tcp://localhost:{port}/Container")
            );
            _serviceHost.Open();
            Console.WriteLine($"Container service is ready and waiting for requests on port: {port}.");
        }

        public void Stop()
        {
            _serviceHost.Close();
            Console.WriteLine("Container service stopped.");
        }
    }
}
