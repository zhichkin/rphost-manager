using System.Collections.Generic;

namespace rphost_manager
{
    public sealed class AppSettings
    {
        public int LogSize { get; set; } = 64 * 1024; // 64 Kb = 65536 bytes
        public string CLSID { get; set; } = string.Empty;
        public string ServerAddress { get; set; } = string.Empty; // tcp://Zhichkin:1540
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int InspectionPeriodicity { get; set; } = 600; // seconds
        public int WorkingServerResetWaitTime { get; set; } = 10; // seconds
        public int WorkingProcessMemoryLimit { get; set; } = 4194304; // kilobytes = 4 Gb
        public Dictionary<string, int> WorkingServerMemoryLimits { get; set; } = new Dictionary<string, int>();
    }
}