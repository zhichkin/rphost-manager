namespace rphost_manager
{
    internal sealed class ResetInfo
    {
        internal string HostName { get; set; } = string.Empty;
        internal long TotalMemory { get; set; } = 0;
        internal bool Reset { get; set; } = false;
        public override string ToString()
        {
            return HostName + " (" + TotalMemory.ToString() + ")";
        }
    }
}