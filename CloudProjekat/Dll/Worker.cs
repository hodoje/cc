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

            MyAssemblyName = GetAssemblyName();

            RoleEnvironmentDll = Assembly.LoadFile(MyAssemblyName);

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

        public string GetAssemblyName()
        {
            string executingExe = Assembly.GetCallingAssembly().Location;
            string debugDir = Path.GetDirectoryName(executingExe);
            string binDir = Path.GetDirectoryName(debugDir);
            string consoleAppPath = Path.GetDirectoryName(binDir);
            string myAssemblyDirectory = Path.GetFullPath(consoleAppPath + $@"\Folder{ExecutingContainerId}");
            string assemblyName = Directory.GetFiles(myAssemblyDirectory).First(x => x.Contains("RoleEnvironmentDll.dll"));
            return assemblyName;
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
                        Type workerClass = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "RoleEnvironment");
                        Type iWorkerInterface = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "IRoleEnvironment");
                        if (workerClass.GetInterfaces().Contains(iWorkerInterface))
                        {
                            string typeName = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "RoleEnvironment").FullName;
                            object obj = RoleEnvironmentDll.CreateInstance(typeName);
                            if (obj != null)
                            {
                                System.Reflection.MethodInfo mi = obj.GetType().GetMethod("ReturnAddress");

                                result = (string)(mi.Invoke(obj, new object[2] { $"{myAssemblyName}", containerId }));
                            }
                        }
                        else
                        {
                            result = "Dll has no IRoleEnvironment interface and a class that implements it.";
                        }
                    }
                    RoleEnvironmentDll = null;
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
                        Type workerClass = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "RoleEnvironment");
                        Type iWorkerInterface = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "IRoleEnvironment");
                        if (workerClass.GetInterfaces().Contains(iWorkerInterface))
                        {
                            string typeName = RoleEnvironmentDll.ExportedTypes.ToList().Find(x => x.Name == "RoleEnvironment").FullName;
                            object obj = RoleEnvironmentDll.CreateInstance(typeName);
                            if (obj != null)
                            {
                                System.Reflection.MethodInfo mi = obj.GetType().GetMethod("ReturnBrotherInstancesAddresses");

                                result = (string[])(mi.Invoke(obj, new object[2] { $"{myAssemblyName}", myAddress.Split(':')[1] }));
                            }
                        }
                        else
                        {
                            result = null;
                        }
                    }
                    RoleEnvironmentDll = null;
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
