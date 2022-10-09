using CommandLine;
using LibTF2AutoClipper;
using LibTF2AutoClipper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace TF2AutoClipper_Console
{
    internal class Program
    {
        public class Options
        {
            [Option('i', "input", Required = true, HelpText = "Single Demo File to be processed.")]
            public string demoPath { get; set; }
        }
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    using IHost host = Host.CreateDefaultBuilder()
                        .ConfigureServices((_, services) =>
                        {
                            services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Trace))
                                .AddSingleton<IRCONService, RCONService>()
                                .AddSingleton<IDemoRecorder, DemoRecorder>()
                                .AddSingleton<IObsController, ObsController>()
                                .AddSingleton<IGameLauncher, GameLauncher>();
                        })
                        .Build();
                    if (o.demoPath != null)
                    {
                        Console.WriteLine("Processing Demo: " + o.demoPath);
                        ProcessDemo(o.demoPath, host.Services);
                    }
                });
        }

        public static void ProcessDemo(string demoPath, IServiceProvider services)
        {
            using var serviceScope = services.CreateScope();
            var provider = serviceScope.ServiceProvider;
            
            var demoRecorder = provider.GetRequiredService<IDemoRecorder>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            var demoQueue = new Queue<DemoFileInfo>();
            var demoPaths = demoPath.Split(';');
            foreach (var path in demoPaths)
            {
                demoQueue.Enqueue(FileUtil.CreateDemoFileInfoFromDemoPath(path));
            }

            AsyncContext.Run(() => demoRecorder.RecordDemos(demoQueue, new CancellationToken(false)));
        }
    }
}