using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace rphost_manager
{
    public sealed class WorkerService : BackgroundService
    {
        private bool _stopping = false;
        private object _stoppingSyncRoot = new object();

        private HostOptions Options { get; set; }
        private AppSettings Settings { get; set; }
        private IServiceProvider Services { get; set; }
        private IRpHostManagerService RpHostManager { get; set; }
        public WorkerService(IServiceProvider serviceProvider, IOptions<AppSettings> settings, IOptions<HostOptions> options)
        {
            Options = options.Value;
            Settings = settings.Value;
            Services = serviceProvider;
            RpHostManager = Services.GetService<IRpHostManagerService>();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log("rphost manager is started.");
            return base.StartAsync(cancellationToken);
        }
        
        private void Stop()
        {
            lock (_stoppingSyncRoot)
            {
                _stopping = true;
            }
        }
        private bool IsStopping()
        {
            lock (_stoppingSyncRoot)
            {
                return _stopping;
            }
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log("rphost manager is stopping ...");
            try
            {
                // prevent running new inspection
                Stop();
                // wait for restoring of working server settings to initial state (see ResetWorkingServer method)
                Task.Delay(Options.ShutdownTimeout, cancellationToken).Wait();
            }
            catch (Exception error)
            {
                FileLogger.Log(ExceptionHelper.GetErrorText(error));
            }
            FileLogger.Log("rphost manager is stopped.");
            return base.StopAsync(cancellationToken);
        }
        
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Running the job in the background
            
            //_ = Task.Run(async () => { await DoWorkAsync(cancellationToken); }, cancellationToken);

            _ = Task.Factory.StartNew(DoWorkAsync, cancellationToken,
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            // Return completed task to let other services to run
            return Task.CompletedTask;
        }
        private async Task DoWorkAsync(object cancellationToken)
        {
            await DoWorkAsync((CancellationToken)cancellationToken);
        }
        private async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (IsStopping()) return;
                try
                {
                    RpHostManager.InspectWorkingProcesses();
                }
                catch (Exception error)
                {
                    FileLogger.Log(ExceptionHelper.GetErrorText(error));
                }
                await Task.Delay(TimeSpan.FromSeconds(Settings.InspectionPeriodicity), cancellationToken);
            }
        }
    }
}