using ServiceAppDMSRecieving;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>//(context, services) =>
    {
        //var configuration = context.Configuration;

        //string preference = configuration["ConnectionStrings:DMS"];

        services.AddTransient<ImmsRCRcsvFile, mmsRCRcsvFileService>();
        services.AddTransient<ImmsPORAcsvFile, mmsPORAcsvFileService>();
        services.AddTransient<IAudilogs, AudilogsService>();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
