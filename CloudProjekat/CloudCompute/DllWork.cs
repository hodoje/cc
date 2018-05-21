using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudCompute
{
    public class DllWork : FileWork
    {
        public DllWork() { }

        public string CopyDllToContainerFolder(int numOfContainers, string packetName, int startingContainerIdx, string rootDirectoryPath, string containersPartialDirectoryPath)
        {
            string executingDllSourcePath;
            FileInfo[] allDlls = ReturnAllDlls(packetName, rootDirectoryPath, out executingDllSourcePath);

            if (!String.IsNullOrWhiteSpace(executingDllSourcePath))
            {
                int cnt = 0;
                int i = startingContainerIdx;

                while (cnt < numOfContainers)
                {
                    foreach (FileInfo dll in allDlls)
                    {
                        File.Copy(dll.FullName, $@"{containersPartialDirectoryPath}{i}\{Path.GetFileName(dll.Name)}", true);
                        //File.Copy(dllSourcePath, $@"{containersPartialDirectoryPath}{i}\{Path.GetFileName(dllSourcePath)}", true);
                    }
                    cnt++;
                    i = ((i + 1) == 4) ? 0 : i + 1;
                }
                return $@"{containersPartialDirectoryPath}?\{Path.GetFileName(executingDllSourcePath)}";
            }
            return "";
        }

        public FileInfo[] ReturnDlls(string packetName, string rootDirectoryPath)
        {
            if (CheckIfRootDirectoryContainsPackets(rootDirectoryPath))
            {
                DirectoryInfo rootDirectoryInfo = new DirectoryInfo(rootDirectoryPath);
                DirectoryInfo[] subDirectories = rootDirectoryInfo.GetDirectories(packetName);

                string filter = "*.dll";
                FileInfo[] listOfFiles = subDirectories[0].GetFiles(filter).ToArray();
                return listOfFiles;
            }
            return null;
        }

        public FileInfo[] ReturnAllDlls(string packetName, string rootDirectoryPath, out string executingDllFileName)
        {
            FileInfo[] listOfFiles = ReturnDlls(packetName, rootDirectoryPath);
            executingDllFileName = (listOfFiles.Length > 4) ? "" : ReturnExecutingDllFileName(listOfFiles);
            return listOfFiles;
        }

        public string ReturnExecutingDllFileName(FileInfo[] listOfFiles)
        {
            string result = "";
            foreach (FileInfo file in listOfFiles)
            {
                if (file.Name == "Dll.dll")
                {
                    result = file.FullName;
                    break;
                }
            }
            return result;
        }
        
        public void CopyDllToContainerFolder(string source, string destination)
        {
            if (!File.Exists(source))
            {
                File.Copy(source, destination);
            }
        }
    }
}
