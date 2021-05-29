using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
        private const string CONST_CLSID = "181E893D-73A4-4722-B61D-D604B3D67D47";
        private const string CONST_PROGID = "V83.COMConnector";

        private object _connector;
        private object _serverAgent;

        private AppSettings Settings { get; set; }
        private IServiceProvider Services { get; set; }
        public RpHostManagerService(IServiceProvider serviceProvider, IOptions<AppSettings> options)
        {
            Settings = options.Value;
            Services = serviceProvider;
        }
        private void Dispose()
        {
            if (_connector != null)
            {
                Marshal.FinalReleaseComObject(_connector);
                _connector = null;
            }
            if (_serverAgent != null)
            {
                Marshal.FinalReleaseComObject(_serverAgent);
                _serverAgent = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        private void DisposeComObjects(object[] comObjects)
        {
            for (int i = 0; i < comObjects.Length; i++)
            {
                if (comObjects[i] != null)
                {
                    Marshal.FinalReleaseComObject(comObjects[i]);
                    comObjects[i] = null;
                }
            }
        }
        
        public void InspectWorkingProcesses()
        {
            if (!InitializeConnector()) return;
            if (!InitializeServerAgent()) return;

            object[] clusters = GetClusters();
            if (clusters == null || clusters.Length == 0)
            {
                FileLogger.Log("List of clusters is empty.");
                return;
            }

            foreach (object cluster in clusters)
            {
                InspectCluster(cluster); // IClusterInfo
            }
            DisposeComObjects(clusters);

            Dispose();
        }
        
        private bool InitializeConnector()
        {
            if (_connector != null) return true;

            Type connectorType;
            if (string.IsNullOrWhiteSpace(Settings.CLSID))
            {
                connectorType = Type.GetTypeFromProgID(CONST_PROGID);
            }
            else
            {
                connectorType = Type.GetTypeFromCLSID(new Guid(Settings.CLSID));
            }

            if (connectorType == null)
            {
                if (string.IsNullOrWhiteSpace(Settings.CLSID))
                {
                    FileLogger.Log("ProgID \"" + CONST_PROGID + "\" is not found.");
                }
                else
                {
                    FileLogger.Log("CLSID {" + Settings.CLSID + "} is not found.");
                }
                return false;
            }

            try
            {
                _connector = Activator.CreateInstance(connectorType);
                FileLogger.Log(CONST_PROGID + " initialized successfully.");
            }
            catch (Exception error)
            {
                FileLogger.Log(ExceptionHelper.GetErrorText(error));
            }

            return (_connector != null);
        }
        private bool InitializeServerAgent()
        {
            if (_connector == null) throw new InvalidOperationException(CONST_PROGID + " is not initialized.");

            if (_serverAgent != null) return true;

            FileLogger.Log("Connecting " + Settings.ServerAddress + " ...");

            Type proxy = _connector.GetType();

            try
            {
                _serverAgent = proxy.InvokeMember("ConnectAgent",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, _connector,
                    new object[] { Settings.ServerAddress }); // IServerAgentConnection

                FileLogger.Log("Connected " + Settings.ServerAddress + " successfully.");
            }
            catch (Exception error)
            {
                FileLogger.Log("Failed to connect " + Settings.ServerAddress + "."
                    + Environment.NewLine + ExceptionHelper.GetErrorText(error));
            }

            return (_serverAgent != null);
        }

        private object[] GetClusters()
        {
            if (_connector == null) throw new InvalidOperationException(CONST_PROGID + " is not initialized.");
            if (_serverAgent == null) throw new InvalidOperationException("Server agent " + Settings.ServerAddress + " is not initialized.");

            Type proxy = _serverAgent.GetType();

            object[] clusters = null;
            try
            {
                clusters = (object[])proxy.InvokeMember("GetClusters",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                    null); // COMSafeArray of IClusterInfo
            }
            catch (Exception error)
            {
                FileLogger.Log(ExceptionHelper.GetErrorText(error));
            }
            return clusters;
        }
        private void InspectCluster(object cluster)
        {
            Type clusterProxy = cluster.GetType();

            string clusterName = (string)clusterProxy.InvokeMember("ClusterName",
                BindingFlags.Public | BindingFlags.GetProperty, null, cluster,
                null);

            FileLogger.Log("Inspecting cluster [" + clusterName + "] on " + Settings.ServerAddress + " ...");

            if (!AuthenticateWithCluster(cluster))
            {
                FileLogger.Log("Inspection of cluster [" + clusterName + "] on " + Settings.ServerAddress + " is finished.");
                return;
            }

            Dictionary<string, ResetInfo> serversToReset = GetWorkingServersToReset(cluster);
            if (serversToReset.Count == 0)
            {
                FileLogger.Log("Working process memory limit is not exceeded.");
                return;
            }

            string text = "List of working servers to be reset:";
            foreach (ResetInfo info in serversToReset.Values)
            {
                text += Environment.NewLine + info.HostName;
            }
            FileLogger.Log(text);

            object[] workingServers = GetWorkingServers(cluster);
            if (workingServers == null || workingServers.Length == 0)
            {
                FileLogger.Log("List of working servers for cluster [" + clusterName + "] on " + Settings.ServerAddress + " is empty.");
                return;
            }
            foreach (object workingServer in workingServers)
            {
                ResetWorkingServer(cluster, workingServer, serversToReset);
            }
            DisposeComObjects(workingServers);

            FileLogger.Log("Inspection of cluster [" + clusterName + "] on " + Settings.ServerAddress + " is finished.");
        }
        private bool AuthenticateWithCluster(object cluster)
        {
            Type clusterProxy = cluster.GetType();

            string clusterName = (string)clusterProxy.InvokeMember("ClusterName",
                BindingFlags.Public | BindingFlags.GetProperty, null, cluster,
                null);

            FileLogger.Log("Authenticating with [" + clusterName + "] ...");

            Type agentProxy = _serverAgent.GetType();

            try
            {
                _ = agentProxy.InvokeMember("Authenticate",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                    new object[] { cluster, Settings.UserName, Settings.Password });

                FileLogger.Log("Authenticated with [" + clusterName + "] successfully.");
            }
            catch (Exception error)
            {
                FileLogger.Log("Failed to authenticate with cluster [" + clusterName + "]"
                    + Environment.NewLine + ExceptionHelper.GetErrorText(error));
                return false;
            }

            return true;
        }
        
        private Dictionary<string, ResetInfo> GetWorkingServersToReset(object cluster)
        {
            Dictionary<string, ResetInfo> serversToReset = new Dictionary<string, ResetInfo>();

            Type agentProxy = _serverAgent.GetType();

            object[] workingProcesses = (object[])agentProxy.InvokeMember("GetWorkingProcesses",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                new object[] { cluster });

            if (workingProcesses == null || workingProcesses.Length == 0)
            {
                return serversToReset;
            }

            foreach (object workingProcess in workingProcesses)
            {
                Type proxy = workingProcess.GetType();

                string pid = (string)proxy.InvokeMember("PID", BindingFlags.Public | BindingFlags.GetProperty, null, workingProcess, null);
                bool enabled = (bool)proxy.InvokeMember("IsEnable", BindingFlags.Public | BindingFlags.GetProperty, null, workingProcess, null);
                int memorySize = (int)proxy.InvokeMember("MemorySize", BindingFlags.Public | BindingFlags.GetProperty, null, workingProcess, null);
                string hostName = (string)proxy.InvokeMember("HostName", BindingFlags.Public | BindingFlags.GetProperty, null, workingProcess, null);

                if (!enabled) continue; // Неактивные рабочие процессы не учитываем

                if (Settings.WorkingServerMemoryLimits.Count > 0
                    && !Settings.WorkingServerMemoryLimits.ContainsKey(hostName))
                {
                    continue; // Рабочий процесс не принадлежит инспектируемым рабочим серверам - не учитываем
                }

                ResetInfo info;
                if (serversToReset.TryGetValue(hostName, out info))
                {
                    info.TotalMemory += memorySize * 1024; // bytes
                }
                else
                {
                    info = new ResetInfo()
                    {
                        Reset = false,
                        HostName = hostName,
                        TotalMemory = memorySize * 1024 // bytes
                    };
                    serversToReset.Add(hostName, info);
                }

                bool exceeded = IsWorkingProcessExceedsMemoryLimit(hostName, memorySize, out int memoryLimit);

                if (exceeded)
                {
                    info.Reset = exceeded;
                    FileLogger.Log(hostName + ", PID " + pid
                        + " (" + memorySize.ToString() + " Kb)"
                        + " exceeds memory limit of " + memoryLimit.ToString() + " Kb.");
                }
            }
            DisposeComObjects(workingProcesses);

            Dictionary<string, ResetInfo> result = new Dictionary<string, ResetInfo>();
            foreach (var item in serversToReset)
            {
                if (item.Value.Reset)
                {
                    result.Add(item.Key, item.Value);
                }
            }
            serversToReset.Clear();

            return result;
        }
        private bool IsWorkingProcessExceedsMemoryLimit(string hostName, int memorySize, out int memoryLimit)
        {
            if (Settings.WorkingServerMemoryLimits.TryGetValue(hostName, out memoryLimit))
            {
                if (memorySize > memoryLimit)
                {
                    return true; // Используем индивидуальную настройку для рабочего сервера
                }
            }
            else if (memorySize > Settings.WorkingProcessMemoryLimit)
            {
                memoryLimit = Settings.WorkingProcessMemoryLimit;
                return true; // Используем общую настройку для всех рабочих серверов
            }
            return false;
        }

        private object[] GetWorkingServers(object cluster)
        {
            if (cluster == null) throw new ArgumentNullException(nameof(cluster));
            if (_connector == null) throw new InvalidOperationException(CONST_PROGID + " is not initialized.");
            if (_serverAgent == null) throw new InvalidOperationException("Server agent " + Settings.ServerAddress + " is not initialized.");

            Type proxy = _serverAgent.GetType();

            object[] workingServers = null;
            try
            {
                workingServers = (object[])proxy.InvokeMember("GetWorkingServers",
                    BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                    new object[] { cluster });
            }
            catch (Exception error)
            {
                FileLogger.Log(ExceptionHelper.GetErrorText(error));
            }
            return workingServers;
        }
        private void ResetWorkingServer(object cluster, object workingServer, Dictionary<string, ResetInfo> serversToReset)
        {
            Type agentProxy = _serverAgent.GetType();
            Type serverProxy = workingServer.GetType();

            string hostName = (string)serverProxy.InvokeMember("HostName",
                BindingFlags.Public | BindingFlags.GetProperty, null, workingServer,
                null);

            if (!serversToReset.TryGetValue(hostName, out ResetInfo info))
            {
                return;
            }

            FileLogger.Log("Start to reset working processes on working server " + info.HostName + " ...");

            // get current values

            long currentMemoryLimit = (long)serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemory",
                BindingFlags.Public | BindingFlags.GetProperty, null, workingServer, null); // bytes

            long currentTimeLimit = (long)serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                BindingFlags.Public | BindingFlags.GetProperty, null, workingServer, null); // seconds

            FileLogger.Log("Current memory limit = " + (currentMemoryLimit / 1024).ToString() + " Kb,"
                + " current time limit = " + currentTimeLimit.ToString() + " seconds.");

            // set limit values

            long memoryLimit = info.TotalMemory - info.TotalMemory / 100 * 20; // 20% less than in use

            serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemory",
                BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                new object[] { memoryLimit }); // bytes

            serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                new object[] { 1 }); // seconds

            agentProxy.InvokeMember("UpdateWorkingServer",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                new object[] { cluster, workingServer });

            FileLogger.Log("New memory limit = " + (memoryLimit / 1024).ToString() + " Kb, new time limit = 1 second.");

            // wait for limits to take effect

            FileLogger.Log("Waiting " + Settings.WorkingServerResetWaitTime.ToString() + " seconds for the new settings to take effect ...");

            Task.Delay(TimeSpan.FromSeconds(Settings.WorkingServerResetWaitTime)).Wait();

            // restore default values

            serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemory",
                BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                new object[] { currentMemoryLimit }); // bytes

            serverProxy.InvokeMember("TemporaryAllowedProcessesTotalMemoryTimeLimit",
                BindingFlags.Public | BindingFlags.SetProperty, null, workingServer,
                new object[] { currentTimeLimit }); // seconds

            agentProxy.InvokeMember("UpdateWorkingServer",
                BindingFlags.Public | BindingFlags.InvokeMethod, null, _serverAgent,
                new object[] { cluster, workingServer });

            FileLogger.Log("Restored memory limit = " + (currentMemoryLimit / 1024).ToString() + " Kb,"
                + " restored time limit = " + currentTimeLimit.ToString() + " seconds.");

            FileLogger.Log("Reset of working processes on working server " + info.HostName + " is finished.");
        }
    }
}