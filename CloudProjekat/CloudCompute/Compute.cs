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
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Contract;

namespace CloudCompute
{
    [Serializable]
    public class ConfigData
    {
        private int _instances;


        public ConfigData()
        {
            
        }

        [XmlElement]
        public int Instances
        {
            get { return _instances; }
            set { _instances = value; }
        }
    }

    public class Compute
    {
        private IContainer _proxy;
        private Dictionary<int, Process> _dictOfProcesses;
        private string _rootDirectoryPath;
        private string _containersPartialDirectoryPath;
        private string _containerExe;
        private int _computePort;
        private int _numOfContainers;
        private int _containersStartingPort;
        private FileSystemWatcher _watcher;
        private int _numOfContainersToDoCurrentWork;
        private string _packetsHistoryPath;

        public IContainer Proxy
        {
            get { return _proxy; }
        }

        public Dictionary<int, Process> DictOfProcesses
        {
            get { return _dictOfProcesses; }
            set { _dictOfProcesses = value; }
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

        public Compute()
        {
            _dictOfProcesses = new Dictionary<int, Process>();
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
            var binding = new NetTcpBinding();
            binding.ReliableSession.Enabled = true;            
            binding.ReliableSession.InactivityTimeout = TimeSpan.FromHours(1);
            binding.ReceiveTimeout = TimeSpan.FromHours(1);
            binding.ReliableSession.Ordered = false;
            var factory = new ChannelFactory<IContainer>(
                binding,
                new EndpointAddress($"net.tcp://localhost:{port}/Container")
            );
            _proxy = factory.CreateChannel();
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

                //Thread.Sleep(100);

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
            // Tries to read until it is able to read.
            // Loops until the file is freed from another process
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
                        var a = node;
                        Int32.TryParse(node.InnerText, out value);
                    }
                    return IsValidValue(value) ? value : -1;
                }
                continue;
            }
        }

        private bool IsValidValue(int value)
        {
            return (value > 0 && value <= 4) ? true : false;
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
            //while (true)
            //{
                if (Directory.Exists(_packetsHistoryPath))
                {
                    try
                    {
                        string s = _packetsHistoryPath + '\\' + Path.GetFileName(packetPath);
                        Directory.Move(packetPath, _packetsHistoryPath + '\\' + Path.GetFileName(packetPath));
                        return;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Unable to move executed packet: {Path.GetFileName(packetPath)}");
                    }
                }
            //}
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
                    string dllGenericPath = CopyDllToContainerFolder(_numOfContainersToDoCurrentWork, eventArgs.Name);
                    if (!string.IsNullOrWhiteSpace(dllGenericPath))
                    {
                        Console.WriteLine($"\t\tPacket: {eventArgs.Name} loaded.");
                        Console.WriteLine($"\t\t\tNumber of instances for work: {_numOfContainersToDoCurrentWork}");
                        int cnt = 0;
                        while(cnt < _numOfContainersToDoCurrentWork)
                        {
                            int port = _containersStartingPort + startContainerIdx * 10;
                            Connect(port);
                            string path = $@"{dllGenericPath.Replace("?", startContainerIdx.ToString())}";
                            Console.WriteLine($"\t\t{Proxy.Load(path)}");
                            cnt++;
                            startContainerIdx = ((startContainerIdx + 1) == 4) ? 0 : startContainerIdx + 1;
                        }
                        MovePacketToHistory(eventArgs.FullPath);
                        //for (var i = startContainerIdx; i < _numOfContainersToDoCurrentWork; i++)   // OVDE NAMESTITI!!!
                        //{
                        //    int port = _containersStartingPort + i * 10;
                        //    Connect(port);
                        //    string path = $@"{dllGenericPath.Replace("?", i.ToString())}";
                        //    Console.WriteLine($"\t\t{Proxy.Load(path)}");
                        //    //Thread.Sleep(1000);
                        //}
                        //MovePacketToHistory(eventArgs.FullPath);
                        //startContainerIdx = _numOfContainersToDoCurrentWork;
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
    }
}