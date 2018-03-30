using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dll
{
    public class Worker : IWorker
    {
        public void Start(string containerId)
        {
            Console.WriteLine($"Test{containerId}");
        }

        public void Stop()
        {
            Console.WriteLine("Stop");
        }
    }
}
