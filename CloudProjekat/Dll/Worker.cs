using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dll
{
    public class Worker : IWorker
    {
        private string _executingContainerId;
        private string _ipAddress;

        public void Start(string containerId)
        {
            _executingContainerId = containerId;
            Console.WriteLine($"Test{containerId}");

            string executingExe = Assembly.GetCallingAssembly().Location;
            string debugDir = Path.GetDirectoryName(executingExe);
            string binDir = Path.GetDirectoryName(debugDir);
            string consoleAppPath = Path.GetDirectoryName(binDir);
            string myAssemblyDirectory = Path.GetFullPath(consoleAppPath + $@"\Folder{_executingContainerId}");
            string myAssemblyName = Directory.GetFiles(myAssemblyDirectory).First(x => x.Contains(".dll"));
            _ipAddress = ReturnAddress(myAssemblyName, containerId);
            Console.WriteLine(_ipAddress);
            string[] brotherPorts = ReturnBrotherInstancesAddresses(myAssemblyName, _ipAddress);
            foreach (var brotherPort in brotherPorts)
            {
                Console.WriteLine(brotherPort);
            }
        }

        public void Stop()
        {
            Console.WriteLine("Stop");
        }

        private string ReturnAddress(string myAssemblyName, string containerId)
        {
            var binding = new NetTcpBinding();
            var endpoint = new EndpointAddress(new Uri($"net.tcp://localhost:50000/RoleEnvironment"));
            var factory = new ChannelFactory<IRoleEnvironment>(binding);
            var proxy = factory.CreateChannel(endpoint);
            return proxy.GetAddress(myAssemblyName, containerId);
        }

        private string[] ReturnBrotherInstancesAddresses(string myAssemblyName, string containerId)
        {
            var binding = new NetTcpBinding();
            var endpoint = new EndpointAddress(new Uri($"net.tcp://localhost:50000/RoleEnvironment"));
            var factory = new ChannelFactory<IRoleEnvironment>(binding);
            var proxy = factory.CreateChannel(endpoint);
            return proxy.BrotherInstances(myAssemblyName, containerId);
        }
    }
}
