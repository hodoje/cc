# cc
College subject (Cloud Computing)

NOTE:
  Compute's app.config is assigned with keys for which value is an absolute path!
  
Project stage A translation (originally it's written on Serbian Latin):
  In accordance with the scheme (picture 1), implement a Cloud compute service: 
    a) Create a project with name "ComputeService" that will run a total of 4 container console applications. Console applications
       simulate nodes of one Cloud system. "ComputeService" service has a task to scan a predefined location, to process packets 
       that are made of one .dll and one .xml file and run those .dlls on the container apps. "ComputeService" service does the
       following actions:
          1. Runs 4 processes of a container app that are implemented as console applications, providing them the port on which
             their WCF server will run.
          2. Periodically checks the predefined for new packets. Predefined location has to be configurable (XML file).
             (Which means the path to the predefined locations has to be given in the "ComputeService"'s app.config
          3. Reads an .xml file from a packet and checks for number of instances(of containers) that it needs to run. If the number
             is lower than 1 and greater than 4, it needs to write out a message that the packet's configuration is invalid and 
             delete the packet from predefined location.
          4. It copies the .dll file from the packet on n destinations and engage n containers via WCF service to read the .dll
             and run it, by running the "Start" method from interface IWorkerRole given in the listing 1. Number of n is given in
             step 3.
          5. Each container has its on WCF server with method "Load" given in the listing 2. After reading the .dll, its being ran 
             and checking out, returning back an information about the execution of the .dll (was it ok or if there was an error).
          6. NOTE: All WCF servers are ran on localhost address, but on different ports. For simplicity of the solution, it is
             allowed to use predefined ports. Example of ports interval is: 10010 - 10050.
