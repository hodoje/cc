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
        private string _myAssemblyName;
        private Assembly _roleEnvironmentDll;

        public string ExecutingContainerId { get => _executingContainerId; set => _executingContainerId = value; }
        public string IpAddress { get => _ipAddress; set => _ipAddress = value; }
        public string MyAssemblyName { get => _myAssemblyName; set => _myAssemblyName = value; }
        public Assembly RoleEnvironmentDll { get => _roleEnvironmentDll; set => _roleEnvironmentDll = value; }

        public void Start(string containerId)
        {
            ExecutingContainerId = containerId;
            Console.WriteLine($"Test{ExecutingContainerId}");

            MyAssemblyName = GetAssemblyFullName("Dll.dll");

            RoleEnvironmentDll = Assembly.LoadFile(GetAssemblyFullName("RoleEnvironmentDll.dll"));

            IpAddress = ReturnAddress(MyAssemblyName, ExecutingContainerId);
            Console.WriteLine(IpAddress);

            string[] brotherPorts = ReturnBrotherInstancesAddresses(MyAssemblyName, IpAddress);
            foreach (var brotherPort in brotherPorts)
            {
                Console.WriteLine(brotherPort);
            }
        }

        public void Stop()
        {
            Console.WriteLine("Stop");
        }

        public string GetAssemblyFullName(string assemblyName)
        {
            string executingExe = Assembly.GetCallingAssembly().Location;
            string debugDir = Path.GetDirectoryName(executingExe);
            string consoleAppPath = Path.GetDirectoryName(debugDir);
            string myAssemblyDirectory = Path.GetFullPath(consoleAppPath + $@"\Folder{ExecutingContainerId}");
            string fullAssemblyName = Directory.GetFiles(myAssemblyDirectory).FirstOrDefault(x => x.Contains(assemblyName));
            return fullAssemblyName;
        }

        private string ReturnAddress(string myAssemblyName, string containerId)
        {
            Task<string> t = new Task<string>(() =>
            {
                string result = "";
                try
                {
                    if (RoleEnvironmentDll != null)
                    {
                        Type workerClass = RoleEnvironmentDll.ExportedTypes.ToList().FirstOrDefault(x => x.Name == "RoleEnvironment");
                        if (workerClass != null)
                        {
                            string typeName = RoleEnvironmentDll.ExportedTypes.ToList().FirstOrDefault(x => x.Name == "RoleEnvironment").FullName;
                            if (!String.IsNullOrWhiteSpace(typeName))
                            {
                                object obj = RoleEnvironmentDll.CreateInstance(typeName);
                                if (obj != null)
                                {
                                    System.Reflection.MethodInfo mi = obj.GetType().GetMethod("ReturnAddress");

                                    result = (string)(mi.Invoke(obj, new object[2] { $"{myAssemblyName}", containerId }));
                                }
                            }
                        }
                        else
                        {
                            result = null;
                        }
                    }
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine(ex.Message);
                    result = null;
                }
                return result;
            });
            t.Start();
            t.Wait();
            return t.Result;
        }

        private string[] ReturnBrotherInstancesAddresses(string myAssemblyName, string myAddress)
        {
            Task<string[]> t = new Task<string[]>(() =>
            {
                string[] result = { };
                try
                {
                    if (RoleEnvironmentDll != null)
                    {
                        Type workerClass = RoleEnvironmentDll.ExportedTypes.ToList().FirstOrDefault(x => x.Name == "RoleEnvironment");
                        if (workerClass != null)
                        {
                            string typeName = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "RoleEnvironment").FullName;
                            if (!String.IsNullOrWhiteSpace(typeName))
                            {
                                object obj = RoleEnvironmentDll.CreateInstance(typeName);
                                if (obj != null)
                                {
                                    System.Reflection.MethodInfo mi = obj.GetType().GetMethod("ReturnBrotherInstancesAddresses");

                                    result = (string[])(mi.Invoke(obj, new object[2] { $"{myAssemblyName}", myAddress }));
                                }
                            }
                        }
                        else
                        {
                            result = null;
                        }
                    }
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine(ex.Message);
                    result = null;
                }
                return result;
            });
            t.Start();
            t.Wait();
            return t.Result;
        }
    }
}
