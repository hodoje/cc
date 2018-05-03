using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Contract;
using System.Linq;
using System.ServiceModel;
using System.Threading;

namespace ConsoleApp
{
    // With ConcurrencyMode.Multiple, threads can call an operation at any time.  
    // It is your responsibility to guard your state with locks. If
    // you always guarantee you leave state consistent when you leave
    // the lock, you can assume it is valid when you enter the lock.
    //[CallbackBehavior(UseSynchronizationContext = false)]
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple)]
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
                            Task tt = Task.Run(() =>
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    Console.WriteLine($"Start{i}");
                                    Thread.Sleep(2000);
                                }
                                Console.WriteLine("Finished.");
                            });
                            tt.Wait();
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
