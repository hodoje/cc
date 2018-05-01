using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudCompute
{
    public class ContainerData
    {
        private int _id;
        private int _port;
        private string _containerRootDirectory;

        public ContainerData(int id, int port, string containerRootDirectory)
        {
            Id = id;
            Port = port;
            ContainerRootDirectory = containerRootDirectory;
        }

        public int Id { get => _id; set => _id = value; }
        public int Port { get => _port; set => _port = value; }
        public string ContainerRootDirectory { get => _containerRootDirectory; set => _containerRootDirectory = value; }
    }
}
