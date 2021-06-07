using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using FileAuditService.Core;
using FileAuditService.Core.Interfaces;
using FileAuditService.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FileAuditService
{
    public static class Program
    {
#if DEBUG
        private const string DefaultEnvironmentName = "Development";
#else
        private const string DefaultEnvironmentName = "Production";
#endif
        public static void Main(string[] args)
        {
            try
            {
                //Read Configuration from appSettings
                string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
                if (string.IsNullOrEmpty(environment))
                    environment = DefaultEnvironmentName;
                IConfigurationRoot config = new ConfigurationBuilder()
                    .AddJsonFile($"appsettings.{environment}.json", true, true)
                    .AddEnvironmentVariables()
                    .Build();
                //Initialize Logger
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(config)
                    .CreateLogger();
                Log.Information($"File Audit Service Starting: {Dns.GetHostName()}");
                Log.Information($"Environment: {environment}");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(loggingConfiguration => loggingConfiguration.ClearProviders())
                .UseSerilog()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
                    if (string.IsNullOrEmpty(environment))
                        environment = DefaultEnvironmentName;
                    configApp.AddJsonFile($"appsettings.{environment}.json", true);
                    configApp.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    IConfiguration configuration = hostContext.Configuration;
                    hostContext.HostingEnvironment.ApplicationName = "File Audit Service";
                    AuditorSettings auditorSettings =
                        hostContext.Configuration.GetSection("AuditorSettings").Get<AuditorSettings>();
                    services.AddSingleton<IAuditorSettings>(auditorSettings);
                    services.AddSingleton<IAuditor, Auditor>();
                    services.AddHostedService<Worker>();
                });
        }
    }
}
