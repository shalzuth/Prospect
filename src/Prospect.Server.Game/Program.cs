using Prospect.Unreal.Core;
using Prospect.Unreal.Runtime;
using Serilog;

namespace Prospect.Server.Game;

internal static class Program
{
    private const float TickRate = (1000.0f / 60.0f) / 1000.0f;
    
    private static readonly ILogger Logger = Log.ForContext(typeof(Program));
    private static readonly PeriodicTimer ServerTick = new PeriodicTimer(TimeSpan.FromSeconds(TickRate));
    private static readonly PeriodicTimer ClientTick = new PeriodicTimer(TimeSpan.FromSeconds(TickRate));
    
    public static async Task Main()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            ServerTick.Dispose();
            ClientTick.Dispose();
            e.Cancel = true;
        };
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext,-52}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        Logger.Information("Starting Prospect.Server.Game");

        // Prospect:
        //  Map:        /Game/Maps/MP/Station/Station_P
        //  GameMode:   /Script/Prospect/YGameMode_Station

        var worldUrl = new FUrl
        {
            Map = "/Game/ThirdPersonCPP/Maps/ThirdPersonExampleMap",
            //Port = 17777
        };

        var runServer = false;
        var runClient = true;

        if (runServer && runClient)
        {
            RunServer(worldUrl);
            Thread.Sleep(100);
            RunClient(worldUrl);
            Console.ReadLine();
        }
        else if (runServer)
        {
            await RunServer(worldUrl);
        }
        else if (runClient)
        {
            await RunClient(worldUrl);
        }


        Logger.Information("Shutting down");
    }
    public static async Task RunServer(FUrl worldUrl)
    {
        await using (var world = new ProspectWorld())
        {
            world.SetGameInstance(new UGameInstance());
            world.SetGameMode(worldUrl);
            world.InitializeActorsForPlay(worldUrl, true);
            world.Listen();

            try
            {
                while (await ServerTick.WaitForNextTickAsync())
                {
                    world.Tick(TickRate);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("server crash : " + ex.Message);
            }
        }
        Logger.Information("Server Closing");
    }
    public static async Task RunClient(FUrl worldUrl)
    {
        await using (var client = new NetGameWorld())
        {
            client.Connect(new FUrl { Host = System.Net.IPAddress.Loopback, Port = worldUrl.Port });

            try
            {
                while (await ClientTick.WaitForNextTickAsync())
                {
                    client.Tick(TickRate);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("client crash : " + ex.Message);
            }
        }
        Logger.Information("Client Closing");
    }
}