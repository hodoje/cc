using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Contract;

namespace CloudCompute
{
    public class Compute
    {
        private XmlWork _xmlParser;
        private DllWork _dllParser;
        private Dictionary<int,IContainer> _proxyDictionary;
        private RoleEnvironment _roleEnvironment;
        private bool _areContainersExecuting;
        private int _startingContainerIdx;
        private string _rootDirectoryPath;
        private string _containersPartialDirectoryPath;
        private string _containerExe;
        private int _computePort;
        private int _numOfContainers;
        private int _containersStartingPort;
        private FileSystemWatcher _watcher;
        private int _numOfContainersToDoCurrentWork;
        private string _packetsHistoryPath;

        public Dictionary<int, IContainer> ProxyDictionary
        {
            get { return _proxyDictionary; }
        }

        public string RootDirectory
        {
            get { return _rootDirectoryPath; }
        }

        public string ContainersPartialDirectory
        {
            get { return _containersPartialDirectoryPath; }
            set { _containersPartialDirectoryPath = value; }
        }

        public string ContainerExe
        {
            get { return _containerExe; }
            set { _containerExe = value; }
        }

        public int ComputePort
        {
            get { return _computePort; }
            set { _computePort = value; }
        }

        public int NumOfContainers
        {
            get { return _numOfContainers; }
            set { _numOfContainers = value; }
        }

        public int ContainersStartingPort
        {
            get { return _containersStartingPort; }
            set { _containersStartingPort = value; }
        }

        public int NumOfContainersToDoCurrentWork
        {
            get { return _numOfContainersToDoCurrentWork; }
            set { _numOfContainersToDoCurrentWork = value; }
        }

        public string PacketsHistoryPath
        {
            get { return _packetsHistoryPath; }
            set { _packetsHistoryPath = value; }
        }

        public bool AreContainersExecuting
        {
            get { return _areContainersExecuting; }
            set { _areContainersExecuting = value; }
        }

        public int StartingContainerIdx
        {
            get { return _startingContainerIdx; }
            set { _startingContainerIdx = value; }
        }

        public RoleEnvironment RoleEnvironment
        {
            get { return _roleEnvironment; }
            set { _roleEnvironment = value; }
        }

        public XmlWork XmlParser
        {
            get { return _xmlParser; }
            set { _xmlParser = value; }
        }

        public DllWork DllParser
        {
            get { return _dllParser; }
            set { _dllParser = value; }
        }

        public Compute()
        {
            _xmlParser = new XmlWork();
            _dllParser = new DllWork();
            _proxyDictionary = new Dictionary<int, IContainer>();
            _roleEnvironment = new RoleEnvironment();
            _areContainersExecuting = false;
            _startingContainerIdx = 0;
            _rootDirectoryPath = $@"{ConfigurationManager.AppSettings["rootDirectoryPath"]}";
            _containersPartialDirectoryPath = $"{ConfigurationManager.AppSettings["containersPartialDirectoryPath"]}";
            _containerExe = $@"{ConfigurationManager.AppSettings["containerExe"]}";
            Int32.TryParse(ConfigurationManager.AppSettings["computePort"], out _computePort);
            Int32.TryParse(ConfigurationManager.AppSettings["containersStartingPort"], out _containersStartingPort);
            Int32.TryParse(ConfigurationManager.AppSettings["numOfContainers"], out _numOfContainers);
            _packetsHistoryPath = $@"{ConfigurationManager.AppSettings["packetsHistoryPath"]}";
            Task watcherTask = new Task(() =>
            {
                WatchRootDirectory(_rootDirectoryPath);
            });
            watcherTask.Start();
        }

        public void Connect(int port)
        {
            var container = RoleEnvironment.RoleInstances.Values.First(x => x.Port == port);
            if (container != null)
            {
                var binding = new NetTcpBinding();
                // Not enabling ReliableSession enables Force Close of the connection
                //binding.ReliableSession.Enabled = true;
                //binding.ReliableSession.InactivityTimeout = TimeSpan.FromHours(1);
                //binding.ReceiveTimeout = TimeSpan.FromHours(1);
                //binding.ReliableSession.Ordered = false;
                
                var factory = new ChannelFactory<IContainer>(
                    binding,
                    new EndpointAddress($"net.tcp://localhost:{port}/Container")
                );

                if (ProxyDictionary.ContainsKey(container.Id))
                {
                    ProxyDictionary[container.Id] = factory.CreateChannel();
                }
                else
                {
                    ProxyDictionary.Add(container.Id, factory.CreateChannel());
                }
            }
        }

        private void WatchRootDirectory(string directoryPath)
        {
            _watcher = new FileSystemWatcher(directoryPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                Filter = "*.*"
            };
            _watcher.Created += new FileSystemEventHandler(OnNewPacketCreation);
            _watcher.EnableRaisingEvents = true;
        }

        private void RemovePacket(string packetPath)
        {
            try
            {
                DirectoryInfo packetDirectoryInfo = new DirectoryInfo(packetPath);
                FileInfo[] listOfFiles;
                while (true)
                {
                    listOfFiles = packetDirectoryInfo.GetFiles().ToArray();
                    if (listOfFiles.Length > 0 && FileWork.IsFileReady(listOfFiles[0].FullName))
                    {
                        Directory.Delete(packetPath, true);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"Unable to remove packet: {Path.GetDirectoryName(packetPath)}");
            }
        }

        private void MovePacketToHistory(string packetPath)
        {
            if (Directory.Exists(PacketsHistoryPath))
            {
                try
                {
                    string s = PacketsHistoryPath + '\\' + Path.GetFileName(packetPath);
                    Directory.Move(packetPath, PacketsHistoryPath + '\\' + Path.GetFileName(packetPath));
                }
                catch (Exception)
                {
                    Console.WriteLine($"Unable to move executed packet: {Path.GetFileName(packetPath)}");
                }
            }
        }

        private bool CheckIfPacketAlreadyRunned(string packetName)
        {
            try
            {
                if(Directory.Exists(PacketsHistoryPath))
                {
                    DirectoryInfo historyDirectoryInfo = new DirectoryInfo(PacketsHistoryPath);
                    DirectoryInfo[] subDirectoryInfos = historyDirectoryInfo.GetDirectories();

                    foreach (var dir in subDirectoryInfos)
                    {
                        if (dir.Name == packetName)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnNewPacketCreation(object sender, FileSystemEventArgs eventArgs)
        {
            NumOfContainersToDoCurrentWork = XmlParser.ReturnNumberOfContainersForWork(eventArgs.Name, RootDirectory);
            if (CheckIfPacketAlreadyRunned(eventArgs.Name))
            {
                Console.WriteLine($"\t\tPacket: {eventArgs.Name} was already runned and placed in \"History\".");
                Console.WriteLine("\t\t\tRemoving given packet...");
                // Simulation of time of removal, so we can see the file actually gets created and then deleted
                Thread.Sleep(1000);
                RemovePacket(eventArgs.FullPath);
            }
            else
            {
                if (NumOfContainersToDoCurrentWork == -1)
                {
                    Console.WriteLine($"\t\tPacket is invalid: {eventArgs.Name}");
                    Console.WriteLine("\t\t\tRemoving given packet...");
                    // Simulation of time of removal, so we can see the file actually gets created and then deleted
                    Thread.Sleep(1000);
                    RemovePacket(eventArgs.FullPath);
                }
                else
                {
                    string dllGenericPath =
                        DllParser.CopyDllToContainerFolder(NumOfContainersToDoCurrentWork, eventArgs.Name, StartingContainerIdx, RootDirectory, ContainersPartialDirectory);
                    if (!string.IsNullOrWhiteSpace(dllGenericPath))
                    {
                        if(AreContainersExecuting)
                        {
                            Console.WriteLine("\t\tContainers are busy. Try again later.");
                            Console.WriteLine("\t\t\tRemoving given packet...");
                            // Simulation of time of removal, so we can see the file actually gets created and then deleted
                            Thread.Sleep(1000);
                            RemovePacket(eventArgs.FullPath);
                        }
                        else
                        {
                            Console.WriteLine($"\t\tPacket: {eventArgs.Name} loaded.");
                            Console.WriteLine($"\t\t\tNumber of instances for work: {NumOfContainersToDoCurrentWork}");
                            AreContainersExecuting = true;

                            int cnt = 0;
                            Dictionary<int, IContainer> tempProxyList = new Dictionary<int, IContainer>();
                            while (cnt < NumOfContainersToDoCurrentWork)
                            {
                                if(RoleEnvironment.RoleInstances[StartingContainerIdx].CurrentlyExecutingAssemblyName == null)
                                {
                                    tempProxyList.Add(StartingContainerIdx, ProxyDictionary[StartingContainerIdx]);
                                }

                                StartingContainerIdx = ((StartingContainerIdx + 1) == 4) ? 0 : StartingContainerIdx + 1;
                                cnt++;
                            }

                            List<Task> taskArr = new List<Task>();
                            tempProxyList.Values.ToList().ForEach(proxy =>
                            {
                                int idx = -1;
                                foreach (var keyAndValue in tempProxyList)
                                {
                                    if (keyAndValue.Value.Equals(proxy))
                                    {
                                        idx = keyAndValue.Key;
                                        break;
                                    }
                                }

                                string dllPath = $@"{dllGenericPath.Replace("?", idx.ToString())}";                                

                                // This task will run in background for each container
                                Task t = Task.Run(() =>
                                {
                                    // This task will run in the background
                                    Task<string> tt = new Task<string>(() =>
                                    {
                                        try
                                        {
                                            RoleEnvironment.RoleInstances[idx].CurrentlyExecutingAssemblyName = dllPath;
                                            return proxy.Load(dllPath);
                                        }
                                        catch (Exception)
                                        {
                                            RoleEnvironment.RoleInstances[idx].IsOnline = false;
                                            return null;
                                        }
                                    });
                                    tt.Start();

                                    // And the outer task will be blocked until "t" is finished
                                    // Thread will be blocked until there is a "Result" returned
                                    string result = tt.Result;
                                    Console.WriteLine($"\t\t{result}");
                                    if (String.IsNullOrWhiteSpace(result))
                                    {
                                        RoleEnvironment.RoleInstances[idx].CurrentlyExecutingAssemblyName = null;
                                    }
                                    else
                                    {
                                    }
                                    RoleEnvironment.RoleInstances[idx].LastExecutingAssemblyName = dllPath;
                                });
                                taskArr.Add(t);
                            });
                            tempProxyList.Clear();
                            MovePacketToHistory(eventArgs.FullPath);
                        }
                    }
                    else
                    {
                        Console.WriteLine("\t\tPacket is invalid. Too many DLLs.");
                        Console.WriteLine("\t\t\tRemoving given packet...");
                        // Simulation of time of removal, so we can see the file actually gets created and then deleted
                        Thread.Sleep(1000);
                        RemovePacket(eventArgs.FullPath);
                    }
                }
            }
        }

        public void ContainerStateWatcher()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    var proxyDictionaryCopy = new Dictionary<int, IContainer>();
                    foreach (var p in ProxyDictionary)
                    {
                        proxyDictionaryCopy.Add(p.Key, p.Value);
                    }

                    foreach (var keyAndProxy in proxyDictionaryCopy)
                    {
                        try
                        {
                            Console.WriteLine(ProxyDictionary[keyAndProxy.Key].CheckState());                            
                        }
                        catch (Exception)
                        {
                            // Set failed container "IsOnline" status to false immediately
                            RoleEnvironment.RoleInstances[keyAndProxy.Key].IsOnline = false;
                            if(RoleEnvironment.RoleInstances.ToList().FindAll(x => x.Value.IsOnline == true).Count > 0)
                            {
                                HandleIfThereAreFreeContainers(keyAndProxy);
                            }
                            else
                            {
                                HandleIFNoFreeContainers(keyAndProxy);
                            }
                        }
                    }
                    proxyDictionaryCopy.Clear();
                    Thread.Sleep(3000);
                }
            });
        }

        private ContainerData CreateNewContainerFromFailedNoLastDll(int failedContainerId)
        {
            ContainerData newContainer = new ContainerData(failedContainerId,
                RoleEnvironment.RoleInstances[failedContainerId].Port,
                $"{ContainersPartialDirectory}{failedContainerId}", null, null);
            return newContainer;
        }

        private ContainerData CreateNewContainerFromFailedWithLastDll(int failedContainerId)
        {
            ContainerData newContainer = new ContainerData(failedContainerId,
                RoleEnvironment.RoleInstances[failedContainerId].Port,
                $"{ContainersPartialDirectory}{failedContainerId}", null, RoleEnvironment.RoleInstances[failedContainerId].LastExecutingAssemblyName);
            return newContainer;
        }

        private IContainer CreateNewProxyFromOld(int newContainerPort)
        {
            IContainer newProxy;
            var binding = new NetTcpBinding();
            var factory = new ChannelFactory<IContainer>(
                binding,
                new EndpointAddress($"net.tcp://localhost:{newContainerPort}/Container")
            );
            newProxy = factory.CreateChannel();
            return newProxy;
        }

        private Process CreateNewProcessForNewContainer(ContainerData newContainer)
        {
            var newProcess = new Process();
            newProcess.StartInfo.FileName = ContainerExe;
            newProcess.StartInfo.Arguments =
                $"\"{ContainersPartialDirectory}{newContainer.Id}\" {newContainer.Port} {newContainer.Id}";
            return newProcess;
        }

        private void HandleIfThereAreFreeContainers(KeyValuePair<int, IContainer> keyAndProxy)
        {
            // startContainerIdx is used for the round robin principle
            // So, we take te first free container but that is also has to be the logically next one to be used
            //var containerDllStatus = new KeyValuePair<int, bool>();
            int containerDllStatusId;

            if (RoleEnvironment.RoleInstances.ToList().FindAll(x => x.Value.CurrentlyExecutingAssemblyName == null).Count > 1)
            {
                if (RoleEnvironment.RoleInstances[StartingContainerIdx]
                        .CurrentlyExecutingAssemblyName == null)
                {
                    containerDllStatusId = RoleEnvironment.RoleInstances.ToList().First(x => x.Key == StartingContainerIdx).Key;
                }
                else
                {
                    var rightContainerId = RoleEnvironment.RoleInstances.ToList()
                        .Find(x => x.Value.CurrentlyExecutingAssemblyName == null);
                    containerDllStatusId = RoleEnvironment.RoleInstances.ToList().First(x => x.Key == rightContainerId.Value.Id).Key;
                }
            }
            else
            {
                var rightContainerId = RoleEnvironment.RoleInstances.ToList()
                    .Find(x => x.Value.CurrentlyExecutingAssemblyName == null);
                containerDllStatusId = RoleEnvironment.RoleInstances.ToList().First(x => x.Key == rightContainerId.Value.Id).Key;
            }

            int failedContainerId = keyAndProxy.Key;
            // Failed instance now has no currently executing assemblies
            RoleEnvironment.RoleInstances[failedContainerId].CurrentlyExecutingAssemblyName = null;
            int freeContainerId = containerDllStatusId;
            string dllToExecute = RoleEnvironment.RoleInstances[failedContainerId].LastExecutingAssemblyName;

            if (!String.IsNullOrWhiteSpace(dllToExecute))
            {
                try
                {
                    Task t = Task.Run(() =>
                    {
                        // This task will run in the background
                        Task<string> tt = new Task<string>(() =>
                        {
                            try
                            {
                                RoleEnvironment.RoleInstances[freeContainerId].CurrentlyExecutingAssemblyName = dllToExecute;
                                return ProxyDictionary[freeContainerId].Load(dllToExecute);
                            }
                            catch (Exception)
                            {
                                RoleEnvironment.RoleInstances[freeContainerId].IsOnline = false;
                                return null;
                            }
                        });
                        tt.Start();

                        // And the outer task will be blocked until "t" is finished
                        // Thread will be blocked until there is a "Result" returned
                        string result = tt.Result;
                        Console.WriteLine($"\t\t{result}");
                        if (String.IsNullOrWhiteSpace(result))
                        {
                            RoleEnvironment.RoleInstances[freeContainerId].CurrentlyExecutingAssemblyName = null;
                        }
                        else
                        {
                        }                   
                        RoleEnvironment.RoleInstances[freeContainerId].LastExecutingAssemblyName = dllToExecute;
                    });
                }
                catch (Exception)
                {
                    RoleEnvironment.RoleInstances[freeContainerId].CurrentlyExecutingAssemblyName = null;
                    RoleEnvironment.RoleInstances[freeContainerId].IsOnline = false;
                }
            }

            // Place a new container on the failed container's spot, no last dll
            ContainerData newContainer = CreateNewContainerFromFailedNoLastDll(failedContainerId);
            RoleEnvironment.RoleInstances[failedContainerId] = newContainer;

            // Create a new proxy for the container
            IContainer newProxy = CreateNewProxyFromOld(newContainer.Port);

            // Aborting old connection and swaping old proxy for new
            ((IChannel)ProxyDictionary[failedContainerId]).Abort();
            ProxyDictionary[failedContainerId] = newProxy;

            var newProcess = CreateNewProcessForNewContainer(newContainer);
            newProcess.Start();

            // We have to manage the round robin counter if we are using it
            StartingContainerIdx = ((StartingContainerIdx + 1) == 4) ? 0 : StartingContainerIdx + 1;
        }

        private void HandleIFNoFreeContainers(KeyValuePair<int, IContainer> keyAndProxy)
        {
            int failedContainerId = keyAndProxy.Key;
            // Failed container now has no executing assemblies
            RoleEnvironment.RoleInstances[failedContainerId].CurrentlyExecutingAssemblyName = null;
            String dllToExecute = RoleEnvironment.RoleInstances[failedContainerId].LastExecutingAssemblyName;

            // Place a new container on the failed container's spot, there is a dll
            ContainerData newContainer = CreateNewContainerFromFailedWithLastDll(failedContainerId);
            RoleEnvironment.RoleInstances[failedContainerId] = newContainer;

            IContainer newProxy = CreateNewProxyFromOld(newContainer.Port);
            ((IChannel)ProxyDictionary[failedContainerId]).Abort();
            ProxyDictionary[failedContainerId] = newProxy;

            var newProcess = CreateNewProcessForNewContainer(newContainer);
            newProcess.Start();

            if (!String.IsNullOrWhiteSpace(dllToExecute))
            {
                try
                {
                    Task t = Task.Run(() =>
                    {
                        // This task will run in the background
                        Task<string> tt = new Task<string>(() =>
                        {
                            try
                            {
                                RoleEnvironment.RoleInstances[newContainer.Id].CurrentlyExecutingAssemblyName = dllToExecute;
                                return ProxyDictionary[newContainer.Id].Load(dllToExecute);
                            }
                            catch (Exception)
                            {
                                RoleEnvironment.RoleInstances[newContainer.Id].IsOnline = false;
                                return null;
                            }
                        });
                        tt.Start();

                        // And the outer task will be blocked until "t" is finished
                        // Thread will be blocked until there is a "Result" returned
                        string result = tt.Result;
                        Console.WriteLine($"\t\t{result}");
                        if (String.IsNullOrWhiteSpace(result))
                        {
                            RoleEnvironment.RoleInstances[newContainer.Id].CurrentlyExecutingAssemblyName = null;
                        }
                        else
                        {
                        }                   
                        RoleEnvironment.RoleInstances[newContainer.Id].LastExecutingAssemblyName = dllToExecute;
                    });
                }
                catch (Exception)
                {
                    RoleEnvironment.RoleInstances[newContainer.Id].CurrentlyExecutingAssemblyName = null;
                    RoleEnvironment.RoleInstances[newContainer.Id].IsOnline = false;
                }
            }
        }
    }
}