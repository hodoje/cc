using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Contract;

namespace ConsoleApp
{
    public class Container : IContainer
    {
        private string _containerDirectoryPath;
        private int _id;
        private int _port;

        public string ContainerDirectoryPath
        {
            get { return _containerDirectoryPath; }
            set { _containerDirectoryPath = value; }
        }

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        public Container(string containerDirectoryPath, int id, int port)
        {
            _containerDirectoryPath = containerDirectoryPath;
            _id = id;
            _port = port;
        }

        public Container() { }

        public string Load(string assemblyName)
        {
            Task<string> t = new Task<string>(() =>
            {
                string result = "";
                try
                {
                    // If this dll has some dependencies, they won't be loaded.
                    Assembly dll = Assembly.Load(File.ReadAllBytes(assemblyName));
                    if (dll != null)
                    {
                        object obj = dll.CreateInstance("Dll.Worker");
                        if (obj != null)
                        {
                            System.Reflection.MethodInfo mi = obj.GetType().GetMethod("Start");

                            mi.Invoke(obj, new object[1] { $"{Id}" });
                            result = "Dll executed successfully.";
                        }
                    }
                    dll = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    result = $"Dll not executed properly on container: Container{_id}";
                }
                return result;
            });
            t.Start();
            t.Wait();
            return t.Result;
        }

        public string CheckState()
        {
            return $"Container[{Id}] state is OK.";
        }
    }
}
