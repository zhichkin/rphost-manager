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
        private AppSettings Settings { get; set; }
        private IServiceProvider Services { get; set; }
        private IRpHostManagerService RpHostManager { get; set; }
        public WorkerService(IServiceProvider serviceProvider, IOptions<AppSettings> options)
        {
            Settings = options.Value;
            Services = serviceProvider;
            RpHostManager = Services.GetService<IRpHostManagerService>();
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log("rphost manager is started.");

            return base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            FileLogger.Log("rphost manager is stopping ...");
            try
            {
                // TODO: dispose COM objects and restore 1C working servers default settings
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
            // TODO: make it LongRunning
            // Task.Factory.StartNew(RunPullingConsumer, exchange,
            //    _stoppingToken,
            //    TaskCreationOptions.LongRunning,
            //    TaskScheduler.Default);
            _ = Task.Run(async () => { await DoWorkAsync(cancellationToken); }, cancellationToken);
            // Return completed task to let other services to run
            return Task.CompletedTask;
        }
        private async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
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