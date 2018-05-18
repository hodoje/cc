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
            string dllSourcePath = ReturnDllSourcePath(packetName, rootDirectoryPath);
            if (!String.IsNullOrWhiteSpace(dllSourcePath))
            {
                int cnt = 0;
                int i = startingContainerIdx;

                while (cnt < numOfContainers)
                {
                    File.Copy(dllSourcePath, $@"{containersPartialDirectoryPath}{i}\{Path.GetFileName(dllSourcePath)}", true);
                    cnt++;
                    i = ((i + 1) == 4) ? 0 : i + 1;
                }
                return $@"{containersPartialDirectoryPath}?\{Path.GetFileName(dllSourcePath)}";
            }
            return "";
        }

        public FileInfo[] CheckIsValidNumberOfDlls(string packetName, string rootDirectoryPath)
        {
            if (CheckIfRootDirectoryContainsPackets(rootDirectoryPath))
            {
                DirectoryInfo rootDirectoryInfo = new DirectoryInfo(rootDirectoryPath);
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

        public string ReturnDllSourcePath(string packetName, string rootDirectoryPath)
        {
            FileInfo[] listOfFiles = CheckIsValidNumberOfDlls(packetName, rootDirectoryPath);
            return (listOfFiles.Length > 1) ? "" : ReturnDllFileName(listOfFiles);
        }

        public string ReturnDllFileName(FileInfo[] listOfFiles)
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
        
        public void CopyDllToContainerFolder(string source, string destination)
        {
            if (!File.Exists(source))
            {
                File.Copy(source, destination);
            }
        }
    }
}
