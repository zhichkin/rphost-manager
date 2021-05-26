using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;

namespace rphost_manager
{
    public static class Program
    {
        private static IHost _host;
        public static void Main(string[] args)
        {
            _host = CreateHostBuilder(args).Build();
            _host.Run();
        }
        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(ConfigureServices);
        }
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            
            AppSettings settings = ConfigureAppSettings(services);
            FileLogger.LogSize = settings.LogSize;

            services.AddHostedService<WorkerService>();
            services.AddSingleton<IRpHostManagerService, RpHostManagerService>();
        }
        private static AppSettings ConfigureAppSettings(IServiceCollection services)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string catalogPath = Path.GetDirectoryName(asm.Location);

            AppSettings settings = new AppSettings();
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(catalogPath)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            config.Bind(settings);

            services.Configure<AppSettings>(config);
            services.Configure<HostOptions>(config.GetSection("HostOptions"));

            return settings;
        }
    }
}