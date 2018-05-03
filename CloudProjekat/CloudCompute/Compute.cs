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
        private Dictionary<int, bool> _isContainerDllExecutionFinished;
        private Dictionary<int,IContainer> _proxyDictionary;
        private Dictionary<int, ContainerData> _containerDataDictionary;
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

        public Dictionary<int, ContainerData> ContainerDataDictionary
        {
            get { return _containerDataDictionary; }
            set { _containerDataDictionary = value; }
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

        public Dictionary<int, bool> IsContainerDllExecutionFinished
        {
            get { return _isContainerDllExecutionFinished; }
            set { _isContainerDllExecutionFinished = value; }
        }

        public Compute()
        {
            _containerDataDictionary = new Dictionary<int, ContainerData>();
            _proxyDictionary = new Dictionary<int, IContainer>();
            _isContainerDllExecutionFinished = new Dictionary<int, bool>();
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
            var container = _containerDataDictionary.Values.FirstOrDefault(x => x.Port == port);
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

                if (_proxyDictionary.ContainsKey(container.Id))
                {
                    _proxyDictionary[container.Id] = factory.CreateChannel();
                }
                else
                {
                    _proxyDictionary.Add(container.Id, factory.CreateChannel());
                }
            }
        }

        private bool CheckIfRootDirectoryContainsPackets(string rootDirectoryPath)
        {
            try
            {
                Directory.CreateDirectory(rootDirectoryPath);
                DirectoryInfo rootDirectoryInfo = new DirectoryInfo(rootDirectoryPath);
                DirectoryInfo[] subDirectories = rootDirectoryInfo.GetDirectories();

                if (subDirectories.Length == 0)
                {
                    return false;
                }
                else
                {
                    foreach (var subDirectory in subDirectories)
                    {
                        if (subDirectory.Name.Contains("Packet"))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Work with DLL
        private string CopyDllToContainerFolder(int numOfContainers, string packetName)
        {
            string dllSourcePath = ReturnDllSourcePath(packetName);
            if (!String.IsNullOrWhiteSpace(dllSourcePath))
            {
                int cnt = 0;
                int i = startContainerIdx;

                while (cnt < numOfContainers)
                {
                    File.Copy(dllSourcePath, $@"{_containersPartialDirectoryPath}{i}\{Path.GetFileName(dllSourcePath)}", true);
                    cnt++;
                    i = ((i + 1) == 4) ? 0 : i + 1;
                }
                return $@"{_containersPartialDirectoryPath}?\{Path.GetFileName(dllSourcePath)}";
            }
            return "";
        }

        private FileInfo[] CheckIsValidNumberOfDlls(string packetName)
        {
            if (CheckIfRootDirectoryContainsPackets(_rootDirectoryPath))
            {
                DirectoryInfo rootDirectoryInfo = new DirectoryInfo(_rootDirectoryPath);
                DirectoryInfo[] subDirectories = rootDirectoryInfo.GetDirectories(packetName);

                string filter = "*.dll";
                FileInfo[] listOfFiles = subDirectories[0].GetFiles(filter).ToArray();
                if (listOfFiles.Length > 1)
                {
                    return listOfFiles;
                }
                return listOfFiles;
            }
            return null;
        }

        private string ReturnDllSourcePath(string packetName)
        {
            FileInfo[] listOfFiles = CheckIsValidNumberOfDlls(packetName);
            return (listOfFiles.Length > 1) ? "" : ReturnDllFileName(listOfFiles);
        }

        private string ReturnDllFileName(FileInfo[] listOfFiles)
        {
            string result = "";
            foreach (FileInfo file in listOfFiles)
            {
                if (file.Name.Split('.')[1] == "dll")
                {
                    result = file.FullName;
                    break;
                }
            }
            return result;
        }

        // Work with XML
        private int ReturnNumberOfContainersForWork(string packetName)
        {
            try
            {
                if (CheckIfRootDirectoryContainsPackets(_rootDirectoryPath))
                {
                    DirectoryInfo rootDirectoryInfo = new DirectoryInfo(_rootDirectoryPath);
                    DirectoryInfo[] subDirectories = rootDirectoryInfo.GetDirectories(packetName);

                    // The "Create" event is triggered when the folder is created, not when the files are finished copying
                    string filter = "*.xml";
                    FileInfo[] listOfFiles;

                    // Since there is not event when the file is finished it's creation, we check a few times until we get a valid result
                    while (true)
                    {
                        listOfFiles = subDirectories[0].GetFiles(filter).ToArray();
                        if (listOfFiles.Length > 0)
                        {
                            break;
                        }
                    }

                    string configFilename = ReturnConfigFileName(listOfFiles);
                    int numOfInstances = ParseConfigFileForNumOfInstances(configFilename);
                    return numOfInstances;
                }
            }
            catch (Exception)
            {
                return -1;
            }
            return -1;
        }

        private bool IsFileReady(string filename)
        {
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private int ParseConfigFileForNumOfInstances(string path)
        {
            // Tries to read until it is able to read (until it's freed from another process).
            while(true)
            {
                if (IsFileReady(path))
                {
                    string s = File.ReadAllText(path);
                    XmlDocument xmld = new XmlDocument();
                    xmld.LoadXml(s);

                    string xpath = "ConfigData/Instances";
                    var nodes = xmld.SelectNodes(xpath);
                    int value = 0;
                    foreach (XmlNode node in nodes)
                    {
                        Int32.TryParse(node.InnerText, out value);
                    }
                    return IsValidValue(value) ? value : -1;
                }
            }
        }

        private bool IsValidValue(int value)
        {
            return (value > 0 && value <= 4);
        }

        private string ReturnConfigFileName(FileInfo[] listOfFiles)
        {
            string result = "";
            foreach (FileInfo file in listOfFiles)
            {
                if (file.Name.Split('.')[1] == "xml")
                {
                    result = file.FullName;
                    break;
                }
            }
            return result;
        }

        // Other
        private void WatchRootDirectory(string directoryPath)
        {
            _watcher = new FileSystemWatcher(directoryPath);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName;
            _watcher.IncludeSubdirectories = false;
            _watcher.Filter = "*.*";
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
                    if (listOfFiles.Length > 0 && IsFileReady(listOfFiles[0].FullName))
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
            if (Directory.Exists(_packetsHistoryPath))
            {
                try
                {
                    string s = _packetsHistoryPath + '\\' + Path.GetFileName(packetPath);
                    Directory.Move(packetPath, _packetsHistoryPath + '\\' + Path.GetFileName(packetPath));
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
                if(Directory.Exists(_packetsHistoryPath))
                {
                    DirectoryInfo historyDirectoryInfo = new DirectoryInfo(_packetsHistoryPath);
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

        private int startContainerIdx = 0;
        private void OnNewPacketCreation(object sender, FileSystemEventArgs eventArgs)
        {
            _numOfContainersToDoCurrentWork = ReturnNumberOfContainersForWork(eventArgs.Name);
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
                if (_numOfContainersToDoCurrentWork == -1)
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
                        CopyDllToContainerFolder(_numOfContainersToDoCurrentWork, eventArgs.Name);
                    if (!string.IsNullOrWhiteSpace(dllGenericPath))
                    {
                        Console.WriteLine($"\t\tPacket: {eventArgs.Name} loaded.");
                        Console.WriteLine($"\t\t\tNumber of instances for work: {_numOfContainersToDoCurrentWork}");

                        int cnt = 0;
                        Dictionary<int, IContainer> tempProxyList = new Dictionary<int, IContainer>();
                        while (cnt < _numOfContainersToDoCurrentWork)
                        {
                            if (IsContainerDllExecutionFinished[startContainerIdx] == true)
                            {
                                tempProxyList.Add(startContainerIdx, ProxyDictionary[startContainerIdx]);
                            }

                            startContainerIdx = ((startContainerIdx + 1) == 4) ? 0 : startContainerIdx + 1;
                            cnt++;
                        }

                        tempProxyList.Values.ToList().ForEach(x =>
                        {
                            int idx = -1;
                            foreach (var keyAndValue in tempProxyList)
                            {
                                if (keyAndValue.Value.Equals(x))
                                {
                                    idx = keyAndValue.Key;
                                    break;
                                }
                            }

                            string path = $@"{dllGenericPath.Replace("?", idx.ToString())}";
                            ContainerDataDictionary[idx].LastExecutingAssemblyName = path;
                            IsContainerDllExecutionFinished[idx] = false;

                            // This task will run in background for each container
                            Task.Run(() =>
                            {
                                // This task will run in the background
                                Task<string> t = new Task<string>(() =>
                                {
                                    try
                                    {
                                        return x.Load(path);
                                    }
                                    catch (Exception)
                                    {
                                        return "";
                                    }
                                });
                                t.Start();

                                // And the outer task will be blocked until "t" is finished
                                // Thread will be blocked until there is a "Result" returned
                                string result = t.Result;
                                Console.WriteLine($"\t\t{result}");
                                if (String.IsNullOrWhiteSpace(result))
                                {
                                    IsContainerDllExecutionFinished[idx] = false;
                                }
                                else
                                {
                                    IsContainerDllExecutionFinished[idx] = true;
                                }
                            });
                        });
                        tempProxyList.Clear();
                        MovePacketToHistory(eventArgs.FullPath);
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
                            if(IsContainerDllExecutionFinished.ToList().FindAll(x => x.Value == true).Count > 0)
                            {
                                // startContainerIdx is used for the round robin principle
                                // So, we take te first free container but that is also the logical next one to be used
                                var containerDllStatus = new KeyValuePair<int, bool>();

                                if (IsContainerDllExecutionFinished.ToList().FindAll(x => x.Value == true).Count > 1)
                                {
                                    if (IsContainerDllExecutionFinished[startContainerIdx] == true)
                                    {
                                        containerDllStatus =
                                            IsContainerDllExecutionFinished.First(x => x.Key == startContainerIdx);
                                    }
                                    else
                                    {
                                        containerDllStatus =
                                            IsContainerDllExecutionFinished.First(x => x.Value == true);
                                    }
                                }
                                else
                                {
                                    containerDllStatus = IsContainerDllExecutionFinished.First(x => x.Value == true);
                                }

                                Console.WriteLine($"Container[{containerDllStatus.Key}] is free.");
                                int failedContainerId = keyAndProxy.Key;
                                int freeContainerId = containerDllStatus.Key;
                                string dllToExecute = ContainerDataDictionary[failedContainerId].LastExecutingAssemblyName;

                                if (!String.IsNullOrWhiteSpace(dllToExecute))
                                {
                                    try
                                    {
                                        IsContainerDllExecutionFinished[freeContainerId] = false;
                                        ContainerDataDictionary[freeContainerId].LastExecutingAssemblyName = dllToExecute;
                                        Task.Run(() =>
                                        {
                                            try
                                            {
                                                Console.WriteLine($"\t\t{ProxyDictionary[freeContainerId].Load(dllToExecute)}");
                                                IsContainerDllExecutionFinished[freeContainerId] = true;
                                            }
                                            catch (Exception)
                                            {
                                                IsContainerDllExecutionFinished[freeContainerId] = false;
                                            }
                                        });
                                    }
                                    catch (Exception)
                                    {
                                        IsContainerDllExecutionFinished[freeContainerId] = false;
                                        continue;
                                    }
                                }

                                // Place a new container on the failed container's spot, no last dll
                                ContainerData newContainer = CreateNewContainerFromFailedNoLastDll(failedContainerId);
                                ContainerDataDictionary[failedContainerId] = newContainer;

                                // Create a new proxy for the container
                                IContainer newProxy = CreateNewProxyFromOld(newContainer.Port);

                                // Aborting old connection and swaping old proxy for new
                                ((IChannel)ProxyDictionary[failedContainerId]).Abort();
                                ProxyDictionary[failedContainerId] = newProxy;

                                // New container is free for dll execution
                                IsContainerDllExecutionFinished[failedContainerId] = true;

                                var newProcess = CreateNewProcessForNewContainer(newContainer);
                                newProcess.Start();

                                // We have to manage the round robin counter if we are using it
                                startContainerIdx = ((startContainerIdx + 1) == 4) ? 0 : startContainerIdx + 1;
                            }
                            else
                            {
                                int failedContainerId = keyAndProxy.Key;
                                string dllToExecute = ContainerDataDictionary[failedContainerId].LastExecutingAssemblyName;

                                // Place a new container on the failed container's spot, there is a dll
                                ContainerData newContainer = CreateNewContainerFromFailedWithLastDll(failedContainerId);
                                ContainerDataDictionary[failedContainerId] = newContainer;

                                IContainer newProxy = CreateNewProxyFromOld(newContainer.Port);
                                ((IChannel)ProxyDictionary[failedContainerId]).Abort();
                                ProxyDictionary[failedContainerId] = newProxy;

                                var newProcess = CreateNewProcessForNewContainer(newContainer);
                                newProcess.Start();

                                if (!String.IsNullOrWhiteSpace(dllToExecute))
                                {
                                    try
                                    {
                                        IsContainerDllExecutionFinished[newContainer.Id] = false;
                                        ContainerDataDictionary[newContainer.Id].LastExecutingAssemblyName = dllToExecute;
                                        Task.Run(() =>
                                        {
                                            try
                                            {
                                                Console.WriteLine($"Executing a dll on the new container[{newContainer.Id}].");
                                                Console.WriteLine($"\t\t{ProxyDictionary[newContainer.Id].Load(dllToExecute)}");
                                                IsContainerDllExecutionFinished[newContainer.Id] = true;
                                            }
                                            catch (Exception)
                                            {
                                                IsContainerDllExecutionFinished[newContainer.Id] = false;
                                            }
                                        });
                                    }
                                    catch (Exception)
                                    {
                                        IsContainerDllExecutionFinished[newContainer.Id] = false;
                                        continue;
                                    }
                                }
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
                ContainerDataDictionary[failedContainerId].Port,
                $"{ContainersPartialDirectory}{failedContainerId}", "");
            return newContainer;
        }

        private ContainerData CreateNewContainerFromFailedWithLastDll(int failedContainerId)
        {
            ContainerData newContainer = new ContainerData(failedContainerId,
                ContainerDataDictionary[failedContainerId].Port,
                $"{ContainersPartialDirectory}{failedContainerId}", ContainerDataDictionary[failedContainerId].LastExecutingAssemblyName);
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
    }
}