﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Contract;

namespace CloudCompute
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class RoleEnvironment : IRoleEnvironment
    {
        private static int newClientAppPortStep = 200;
        private Dictionary<int, ContainerData> _roleInstances;
        public Dictionary<int, ContainerData> RoleInstances { get => _roleInstances; set => _roleInstances = value; }

        public RoleEnvironment()
        {
            _roleInstances = new Dictionary<int, ContainerData>();
        }

        public string GetAddress(string myAssemblyName, string containerId)
        {
            
            Int32.TryParse(containerId, out var id);
            List<ContainerData> containers;
            string myAssemblyFileName = Path.GetFileName(myAssemblyName);

            // We separate by currently executing assembly name
            // If there is at least one container that is executing the given assembly, we continue extracting all of them
            if (RoleInstances.ToList().Find(x => Path.GetFileName(x.Value.CurrentlyExecutingAssemblyName) == myAssemblyFileName).Value != null)
            {
                containers = RoleInstances.Values.ToList()
                    .FindAll(x => Path.GetFileName(x.CurrentlyExecutingAssemblyName) == myAssemblyFileName);

                // And then we look for a right container
                if (containers.Find(x => x.Id == id) != null)
                {
                    ContainerData rightContainer = containers.Find(x => x.Id == id);
                    // ovde treba da ide neka nova adresa koja ce biti razlicita od svih ostalih kako bi se na njoj podigao worker servis
                    int clientAppPort = rightContainer.Port + newClientAppPortStep;
                    return $"{IPAddress.Loopback}:{clientAppPort}";
                    //return $"{IPAddress.Loopback}:{rightContainer.Port}";
                }
                else
                {
                    return $"There is no container with id: {id} that is executing {myAssemblyName}.";
                }
            }
            else
            {
                return $"There are no containers that are executing {myAssemblyName}";
            }
        }

        public string[] BrotherInstances(string myAssemblyName, string myAddress)
        {
            List<string> portList = new List<string>();
            string myAssemblyFileName = Path.GetFileName(myAssemblyName);

            if (RoleInstances.ToList().FindAll(x => Path.GetFileName(x.Value.CurrentlyExecutingAssemblyName) == myAssemblyFileName).Count > 1)
            {
                foreach (var inst in RoleInstances)
                {
                    string instAssemblyFileName = Path.GetFileName(inst.Value.CurrentlyExecutingAssemblyName);
                    string instPort = $"{inst.Value.Port + newClientAppPortStep}"; 
                    if (instPort != myAddress.Split(':')[1] && instAssemblyFileName == myAssemblyFileName)
                    {
                        portList.Add(inst.Value.Port.ToString());
                    }
                }
            }
            return portList.ToArray();
        }
    }
}
