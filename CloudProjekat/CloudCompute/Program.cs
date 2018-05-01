using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Configuration;
using System.Runtime;
using System.Runtime.Remoting.Channels;

namespace CloudCompute
{
    class Program
    {
        static void Main(string[] args)
        {
            Compute compute = new Compute();
        
            Console.WriteLine("Compute Service started...");
            Console.WriteLine("Creating workers...");

            for (var i = 0; i < compute.NumOfContainers; i++)
            {
                int port = compute.ContainersStartingPort + i * 10;
                var proc = new Process();
                proc.StartInfo.FileName = compute.ContainerExe;
                // If we were to execute the ConsoleApp.exe in CMD with a string argument, we would need to do it like this:
                // ConsoleApp.exe "C:\Users\Nikola Karaklic\Documents\Visual Studio 2017\Projects\CloudProjekat\ConsoleApp\Folder"
                // An argument that is a string neeeds to be in double-quotes when passed, that's why we have these triple double-quotes
                // At the beginning and at the end there are normal double-quotes that make that value a string
                // And after them there are double double-quotes that will add single double-quotes that are needed to pass a valid string argument
                // Those double double-quotes will actualy produce \"
                proc.StartInfo.Arguments = $"\"{compute.ContainersPartialDirectory}{i}\" {port} {i}";                
                proc.Start();
                compute.DictOfProcesses.Add(i, proc);
            }

            while (true)
            {
                Console.WriteLine("Checking designated location...");
                Thread.Sleep(3000);
            }
        }
    }
}
