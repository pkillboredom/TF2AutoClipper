using LibTF2AutoClipper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibTF2AutoClipper
{
    public delegate Task DemoRecorderStateCallback(DemoRecorder sender, DemoRecorderState state);
    
    public class DemoRecorder
    {
        private readonly ILogger<DemoRecorder> _logger;
        private readonly RCONService _rconService;
        private readonly FileUtil _fileUtil;
        private readonly OBSController _obsController;
        private readonly GameLauncher _gameLauncher;
        private readonly IConfiguration _config;
        
        private event DemoRecorderStateCallback DemoRecorderStateChanged;
        private CancellationToken recordCancelToken;

        public DemoRecorder(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetService<ILogger<DemoRecorder>>()!;
            _rconService = serviceProvider.GetService<RCONService>()!;
            _fileUtil = serviceProvider.GetService<FileUtil>()!;
            _obsController = serviceProvider.GetService<OBSController>()!;
            _gameLauncher = serviceProvider.GetService<GameLauncher>()!;
            _config = serviceProvider.GetService<IConfiguration>()!;

            DemoRecorderStateChanged += OnDemoRecorderStateChanged;
            recordCancelToken = new CancellationToken();
        }

        private async Task OnDemoRecorderStateChanged(DemoRecorder sender, DemoRecorderState state)
        {
            switch (state)
            {
                case DemoRecorderState.Idle:
                    return;
                case DemoRecorderState.GameSetup:
                    bool GameIsLaunched = false;
                    // If the game is not launched, launch it.
                    if (_gameLauncher.GameLauncherState == GameLauncherState.NotLaunched
                        || _gameLauncher.GameLauncherState == GameLauncherState.Exited
                        || _gameLauncher.GameLauncherState == GameLauncherState.Error)
                    {
                        var launchTimeout = 30000;
                        var launchTask = _gameLauncher.LaunchGame(_config["Game:GameExePath"], _config["Game:GameDirPath"], "", recordCancelToken);
                        if (await Task.WhenAny(launchTask, Task.Delay(launchTimeout)) == launchTask)
                        {
                            GameIsLaunched = true;
                        }
                        else
                        {
                            GameIsLaunched = false;
                        }
                    }
                    // If configuring or launching, wait.
                    else if (_gameLauncher.GameLauncherState == GameLauncherState.Launching
                        || _gameLauncher.GameLauncherState == GameLauncherState.Configuring)
                    {
                        var launchTimeout = 30000;
                        var launchWaitTask = Task.Run(async () =>
                        { while (_gameLauncher.GameLauncherState != GameLauncherState.Launched
                            || _gameLauncher.GameLauncherState != GameLauncherState.Exited
                            || _gameLauncher.GameLauncherState != GameLauncherState.Error)
                            {
                                await Task.Delay(250);
                            }
                        });
                        if (await Task.WhenAny(launchWaitTask, Task.Delay(launchTimeout)) == launchWaitTask)
                        {
                            if (_gameLauncher.GameLauncherState == GameLauncherState.Launched)
                            {
                                GameIsLaunched = true;
                            }
                            else
                            {
                                GameIsLaunched = false;
                            }
                        }
                        else
                        {
                            GameIsLaunched = false;
                        }
                    }
                    else if (_gameLauncher.GameLauncherState == GameLauncherState.Launched)
                    {
                        GameIsLaunched = true;
                    }
                    if (GameIsLaunched)
                    {
                        _logger.LogInformation("Game is launched. Moving to RconSetup state.");
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.RconSetup);
                    }
                    else
                    {
                        // Launch seems to have failed
                        _logger.LogError("Game launch appears to have failed.");
                        _gameLauncher.ExitGame();
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                        throw new Exception("Game launch appears to have failed, please check logs.");
                    }
                    return;
                case DemoRecorderState.RconSetup:
                    // Set up RCON.
                    bool RconReady = false;
                    ConnectionSettings rconSettings = new ConnectionSettings
                    {
                        Host = _config["rcon:host"],
                        Password = _config["rcon:password"],
                        Port = Convert.ToInt32(_config["rcon:port"])
                    };
                    if (_rconService.ConnectionStatus == RconStatus.Disconnected)
                    {
                        await _rconService.ConnectRCON(rconSettings);
                    }
                    else if (_rconService.ConnectionStatus == RconStatus.Connecting)
                    {
                        // Wait for connected.
                        var rconTimeout = 30000;
                        var rconWaitTask = Task.Run(async () =>
                        {
                            while (_rconService.ConnectionStatus != RconStatus.Connected)
                            {
                                await Task.Delay(250);
                            }
                        });
                        if (await Task.WhenAny(rconWaitTask, Task.Delay(rconTimeout)) == rconWaitTask)
                        {
                            if (_rconService.ConnectionStatus == RconStatus.Connected)
                            {
                                RconReady = true;
                            }
                            else
                            {
                                RconReady = false;
                            }
                        }
                        else
                        {
                            RconReady = false;
                        }
                    }
                    else if (_rconService.ConnectionStatus == RconStatus.Connected)
                    {
                        RconReady = true;
                    }
                    if (RconReady)
                    {
                        _logger.LogInformation("RCON is connected. Moving to OBSSetup");
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.OBSSetup);
                    }
                    else
                    {
                        // RconConnect seems to have failed
                        _logger.LogError("RconConnect appears to have failed.");
                        _gameLauncher.ExitGame();
                        await _rconService.DisconnectRCON();
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                        throw new Exception("RconConnect appears to have failed, please check logs.");
                    }
                    return;
                default:
                    return;
            }
        }

        // TODO: Handle RCON disconnect

        // TODO: Handle OBS disconnect

        // TODO: Handle Game Close

        public Queue<DemoFileInfo> DemoFileInfoListToQueue(List<DemoFileInfo> demoFileInfos)
        {
            var queue = new Queue<DemoFileInfo>();
            foreach (var demoFileInfo in demoFileInfos)
            {
                queue.Enqueue(demoFileInfo);
            }
            return queue;
        }

        public async Task RecordDemos(Queue<DemoFileInfo> demoQueue, CancellationToken cancellationToken) 
        {
            while (demoQueue.Count > 0)
            {
                var demoFileInfo = demoQueue.Dequeue();
                await RecordDemo(demoFileInfo, cancellationToken);
            }
        }

        private async Task RecordDemo(DemoFileInfo demoFileInfo, CancellationToken cancellationToken)
        {
            
            
        }
    }

    public enum DemoRecorderState
    {
        Idle,
        GameSetup,
        RconSetup,
        OBSSetup,
        AllReady,
        DemoLoad,
        DemoReady,
        Recording
    }
}
