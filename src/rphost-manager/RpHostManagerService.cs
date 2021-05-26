using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace rphost_manager
{
    public interface IRpHostManagerService
    {
        void InspectWorkingProcesses();
    }
    public sealed class RpHostManagerService : IRpHostManagerService
    {
        private AppSettings Settings { get; set; }
        private IServiceProvider Services { get; set; }
        public RpHostManagerService(IServiceProvider serviceProvider, IOptions<AppSettings> options)
        {
            Settings = options.Value;
            Services = serviceProvider;
        }
        public void InspectWorkingProcesses()
        {
            string progID = "V83.COMConnector";
            Type t = Type.GetTypeFromProgID(progID);
            //Type t = Type.GetTypeFromCLSID(new Guid("181E893D-73A4-4722-B61D-D604B3D67D47"));
            if (t == null)
            {
                FileLogger.Log(progID + " is not found.");
                return;
            }

            object connector = null;
            try
            {
                connector = Activator.CreateInstance(t);
            }
            catch (Exception error)
            {
                FileLogger.Log(ExceptionHelper.GetErrorText(error));
            }
            if (connector == null) return;

            FileLogger.Log("Connecting " + Settings.ServerAddress + " ...");

            object agent = connector.GetType().InvokeMember("ConnectAgent",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, connector,
                new object[] { Settings.ServerAddress }); // IServerAgentConnection

            if (agent == null)
            {
                FileLogger.Log("Failed to connect " + Settings.ServerAddress + ".");
                return;
            }

            FileLogger.Log("Connected " + Settings.ServerAddress + " successfully.");

            object[] clusters = (object[])agent.GetType().InvokeMember("GetClusters",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                null); // COMSafeArray of IClusterInfo

            int count = clusters.Length;

            if (count == 0)
            {
                FileLogger.Log("List of clusters is empty.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                object cluster = clusters[i]; // IClusterInfo

                string clusterName = (string)cluster.GetType().InvokeMember("ClusterName",
                    BindingFlags.Public | BindingFlags.GetProperty, null, cluster,
                    null);

                FileLogger.Log("Inspecting " + Settings.ServerAddress + " [" + clusterName + "] ...");

                FileLogger.Log("Authenticating with " + clusterName + " ...");

                _ = agent.GetType().InvokeMember("Authenticate",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                    new object[] { cluster, Settings.UserName, Settings.Password });

                FileLogger.Log("Authenticated with " + clusterName + " successfully.");

                object[] workingProcesses = (object[])agent.GetType().InvokeMember("GetWorkingProcesses",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                    new object[] { cluster });

                List<string> serversToReset = GetWorkingServersToResetWorkingProcesses(Settings.WorkingServers, workingProcesses);

                if (serversToReset.Count == 0)
                {
                    FileLogger.Log("Working process memory limit is not exceeded.");
                    return;
                }

                foreach (string serverName in serversToReset)
                {
                    long memoryInUse = GetWorkingServerMemoryInUse(serverName, workingProcesses);
                    
                    object workingServer = GetWorkingServerByName(agent, cluster, serverName);
                    if (workingServer == null)
                    {
                        FileLogger.Log("Working server " + serverName + " is not found.");
                        continue;
                    }

                    FileLogger.Log("Start reset of working processes on working server " + serverName + " ..."
                        + " Total memory in use is " + memoryInUse.ToString() + " Kb.");
                    
                    ResetWorkingServer(agent, cluster, workingServer, memoryInUse);

                    FileLogger.Log("Reset of working processes on working server " + serverName + " is finished.");

                    Marshal.FinalReleaseComObject(workingServer);
                }

                foreach (object workingProcess in workingProcesses)
                {
                    Marshal.FinalReleaseComObject(workingProcess);
                }
                Marshal.FinalReleaseComObject(cluster);

                FileLogger.Log("Inspecting " + Settings.ServerAddress + " [" + clusterName + "] is finished.");
            }

            Marshal.FinalReleaseComObject(agent);
            Marshal.FinalReleaseComObject(connector);

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        private List<string> GetWorkingServersToResetWorkingProcesses(List<string> workingServers, object[] workingProcesses)
        {
            List<string> servers = new List<string>();

            for (int i = 0; i < workingProcesses.Length; i++)
            {
                object wp = workingProcesses[i];

                Type proxy = wp.GetType();

                string pid = (string)proxy.InvokeMember("PID", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);
                int memorySize = (int)proxy.InvokeMember("MemorySize", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);

                if (memorySize > Settings.WorkingProcessMemoryLimit)
                {
                    string hostName = (string)proxy.InvokeMember("HostName", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);

                    if (string.IsNullOrEmpty(servers.Where(s => s == hostName).FirstOrDefault()))
                    {
                        if (workingServers.Count == 0 || !string.IsNullOrEmpty(workingServers.Where(ws => ws == hostName).FirstOrDefault()))
                        {
                            servers.Add(hostName);

                            FileLogger.Log("Host name '" + hostName + "'"
                                + ", working process pid '" + pid + "'"
                                + ", memory usage is " + memorySize.ToString() + " Kb."
                                + " Memory limit of " + Settings.WorkingProcessMemoryLimit.ToString() + " Kb is exceeded.");
                        }
                    }
                }
            }

            return servers;
        }
        private long GetWorkingServerMemoryInUse(string workingServer, object[] workingProcesses)
        {
            long memorySize = 0; // Kb

            for (int i = 0; i < workingProcesses.Length; i++)
            {
                object wp = workingProcesses[i];

                Type proxy = wp.GetType();

                bool enabled = (bool)proxy.InvokeMember("IsEnable", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);
                string hostName = (string)proxy.InvokeMember("HostName", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);

                if (hostName != workingServer) continue;

                if (enabled)
                {
                    memorySize += (int)proxy.InvokeMember("MemorySize", BindingFlags.Public | BindingFlags.GetProperty, null, wp, null);
                }
            }

            return memorySize;
        }

        private object GetWorkingServerByName(object agent, object cluster, string serverName)
        {
            object[] workingServers = (object[])agent.GetType().InvokeMember("GetWorkingServers",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                new object[] { cluster });

            if (workingServers == null || workingServers.Length == 0) return null;

            for (int i = 0; i < workingServers.Length; i++)
            {
                object workingServer = workingServers[i];

                string hostName = (string)workingServer.GetType().InvokeMember("HostName",
                    BindingFlags.Public | BindingFlags.GetProperty, null, workingServer, null);

                if (hostName == serverName)
                {
                    return workingServer;
                }
            }

            return null;
        }
        private void ResetWorkingServer(object agent, object cluster, object workingServer, long memoryInUse)
        {
            long memoryLimit = (memoryInUse * 1024) - (memoryInUse * 1024) / 100 * 15; // 15% less than in use

            // get current values

            long currentMemoryLimit = (long)workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemory",
                        BindingFlags.Public | BindingFlags.GetProperty, null, workingServer, null); // bytes

            long currentTimeWait = (long)workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                    BindingFlags.Public | BindingFlags.GetProperty, null, workingServer, null); // seconds

            FileLogger.Log("Current memory limit = " + currentMemoryLimit.ToString() + " Kb,"
                + " current time limit = " + currentTimeWait.ToString() + " seconds.");

            // set limit values

            workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemory",
                        BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                        new object[] { memoryLimit }); // bytes

            workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                    BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                    new object[] { 1 }); // seconds

            agent.GetType().InvokeMember("UpdateWorkingServer",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                new object[] { cluster, workingServer });

            FileLogger.Log("New memory limit = " + memoryLimit.ToString() + " Kb, new time limit = 1 second.");

            // wait for limits to take effect

            FileLogger.Log("Waiting " + Settings.WorkingServerResetWaitTime.ToString() + " seconds for the new settings to take effect ...");

            Task.Delay(TimeSpan.FromSeconds(Settings.WorkingServerResetWaitTime)).Wait();

            // restore default values

            workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemory",
                    BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                    new object[] { currentMemoryLimit }); // bytes

            workingServer.GetType().InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                    BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                    new object[] { currentTimeWait }); // seconds

            agent.GetType().InvokeMember("UpdateWorkingServer",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, agent,
                new object[] { cluster, workingServer });

            FileLogger.Log("Restored memory limit = " + currentMemoryLimit.ToString() + " Kb,"
                + " restored time limit = " + currentTimeWait.ToString() + " seconds.");
        }
    }
}