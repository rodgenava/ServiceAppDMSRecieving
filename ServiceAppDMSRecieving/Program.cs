//using ServiceAppDMSRecieving;

//IHost host = Host.CreateDefaultBuilder(args)
//    .ConfigureServices(services =>//(context, services) =>
//    {
//        //var configuration = context.Configuration;

//        //string preference = configuration["ConnectionStrings:DMS"];

//        services.AddTransient<ImmsRCRcsvFile, mmsRCRcsvFileService>();
//        services.AddTransient<ImmsPORAcsvFile, mmsPORAcsvFileService>();
//        services.AddTransient<IAudilogs, AudilogsService>();

//        services.AddHostedService<Worker>();
//    })
//    .Build();
//if (IsDevelopment())
//{
//} else if (IsProduction())
//{ 
//}
//await host.RunAsync();

using ServiceAppDMSRecieving;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddTransient<ImmsRCRcsvFile, mmsRCRcsvFileService>();
        services.AddTransient<ImmsPORAcsvFile, mmsPORAcsvFileService>();
        services.AddTransient<IAudilogs, AudilogsService>();

        services.AddHostedService<Worker>();

        // Check if it's in development or production
        var env = context.HostingEnvironment;
        if (env.IsDevelopment())
        {
            // Development-specific services or configurations
        }
        else if (env.IsProduction())
        {
            // Production-specific services or configurations
        }
    })
    .Build();

await host.RunAsync();
