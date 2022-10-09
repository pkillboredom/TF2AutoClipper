using LibTF2AutoClipper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;

namespace LibTF2AutoClipper
{
    public delegate Task DemoRecorderStateCallback(DemoRecorder sender, DemoRecorderState state);

    public interface IDemoRecorder
    {
        Queue<DemoFileInfo> DemoFileInfoListToQueue(List<DemoFileInfo> demoFileInfos);
        Task RecordDemos(Queue<DemoFileInfo> demoQueue, CancellationToken cancellationToken);
    }

    public class DemoRecorder : IDemoRecorder
    {
        private readonly ILogger<DemoRecorder> _logger;
        private readonly IRCONService _rconService;
        private readonly IObsController _obsController;
        private readonly IGameLauncher _gameLauncher;
        private readonly IConfiguration _config;

        private event DemoRecorderStateCallback DemoRecorderStateChanged;
        private CancellationToken recordCancelToken;
        private DemoFileInfo? _currentDemoFileInfo;
        private Task waitForDemoFinishedTask;
        private ConnectionSettings rconSettings;
        private bool demoFinished = false;
        private bool recordingFinished = false;
        private LogReceiver log;

        public DemoRecorder(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<DemoRecorder>>()!;
            _rconService = serviceProvider.GetRequiredService<IRCONService>()!;
            _obsController = serviceProvider.GetRequiredService<IObsController>()!;
            _gameLauncher = serviceProvider.GetRequiredService<IGameLauncher>()!;
            _config = serviceProvider.GetRequiredService<IConfiguration>()!;

            DemoRecorderStateChanged += OnDemoRecorderStateChanged;
            recordCancelToken = new CancellationToken();
            rconSettings = new ConnectionSettings
            {
                Host = _config["rcon:host"],
                Password = _config["rcon:password"],
                Port = Convert.ToInt32(_config["rcon:port"])
            };
        }

        // TODO: add a cancellation token to all tasks that can be cancelled. Cancellation token should be invoked when external state changes unexpectedly.
        private async Task OnDemoRecorderStateChanged(DemoRecorder sender, DemoRecorderState state)
        {

            switch (state)
            {
                case DemoRecorderState.Idle:
                    return;
                case DemoRecorderState.ObsSetup:
                    bool obsReady = false;
                    ConnectionSettings connectionSettings = new ConnectionSettings
                    {
                        Host = _config["obs:host"],
                        Password = _config["obs:password"],
                        Port = Convert.ToInt32(_config["obs:port"])
                    };
                    if (_obsController.Obs == null || !_obsController.Obs.IsConnected)
                    {
                        int obsTimeout = 30000;
                        try
                        {
                            _obsController.ConnectObs(connectionSettings);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"The following exception was thrown while connecting to OBS: {ex}");
                        }
                        var obsWaitTask = Task.Run(async () =>
                        {
                            while (_obsController.Obs == null || !_obsController.Obs.IsConnected)
                            {
                                await Task.Delay(250);
                            }
                        });
                        var delay = Task.Delay(obsTimeout);
                        if (await Task.WhenAny(obsWaitTask, delay) == obsWaitTask)
                        {
                            if (_obsController.Obs != null && _obsController.Obs.IsConnected)
                            {
                                obsReady = true;
                            }
                            else
                            {
                                obsReady = false;
                            }
                        }
                        else
                        {
                            obsReady = false;
                        }
                    }
                    else
                    {
                        obsReady = true;
                    }
                    if (obsReady)
                    {
                        _logger.LogInformation("OBS is connected. Moving to AllReady state.");
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.GameSetup);
                    }
                    else
                    {
                        // OBSConnect seems to have failed
                        _logger.LogError("OBSConnect appears to have failed.");
                        await StopAllModules();
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                        throw new Exception("OBSConnect appears to have failed, please check logs.");
                    }
                    return;
                case DemoRecorderState.GameSetup:
                    bool gameIsLaunched = false;
                    // If the game is not launched, launch it.
                    if (_gameLauncher.GameLauncherState == GameLauncherState.NotLaunched
                        || _gameLauncher.GameLauncherState == GameLauncherState.Exited
                        || _gameLauncher.GameLauncherState == GameLauncherState.Error)
                    {
                        var launchTimeout = 30000;
                        var launchTask = _gameLauncher.LaunchGame(_config["Game:GameExePath"], _config["Game:GameDirPath"], $"{_config["Game:args"]} +rcon_password {_config["rcon:password"]}", recordCancelToken);
                        if (await Task.WhenAny(launchTask, Task.Delay(launchTimeout)) == launchTask)
                        {
                            gameIsLaunched = true;
                        }
                        else
                        {
                            gameIsLaunched = false;
                        }
                    }
                    // If configuring or launching, wait.
                    else if (_gameLauncher.GameLauncherState == GameLauncherState.Launching
                        || _gameLauncher.GameLauncherState == GameLauncherState.Configuring)
                    {
                        var launchTimeout = 30000;
                        var launchWaitTask = Task.Run(async () =>
                        {
                            while (_gameLauncher.GameLauncherState != GameLauncherState.Launched
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
                                gameIsLaunched = true;
                            }
                            else
                            {
                                gameIsLaunched = false;
                            }
                        }
                        else
                        {
                            gameIsLaunched = false;
                        }
                    }
                    else if (_gameLauncher.GameLauncherState == GameLauncherState.Launched)
                    {
                        gameIsLaunched = true;
                    }
                    if (gameIsLaunched)
                    {
                        _logger.LogInformation("Game is launched. Moving to RconSetup state.");
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.RconSetup);
                    }
                    else
                    {
                        // Launch seems to have failed
                        _logger.LogError("Game launch appears to have failed.");
                        await StopAllModules();
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                        throw new Exception("Game launch appears to have failed, please check logs.");
                    }
                    return;
                case DemoRecorderState.RconSetup:
                    // Set up RCON.
                    bool rconReady = false;
                    using (var rconConnectCancelTokenSource = new CancellationTokenSource())
                    {
                        var rconConnectTask = Task.Run(async () =>
                        {
                            while (_rconService.ConnectionStatus != RconStatus.Connected)
                            {
                                await _rconService.ConnectRCON(rconSettings);
                                if (_rconService.ConnectionStatus == RconStatus.Connected)
                                {
                                    break;
                                }

                                await Task.Delay(250);
                            }

                        }, rconConnectCancelTokenSource.Token);
                        // Wait for connected.
                        var rconTimeout = 60000;
                        if (await Task.WhenAny(rconConnectTask, Task.Delay(rconTimeout)) == rconConnectTask)
                        {
                            if (_rconService.ConnectionStatus == RconStatus.Connected)
                            {
                                rconReady = true;
                                rconConnectCancelTokenSource.Cancel();
                            }
                            else
                            {
                                rconReady = false;
                                rconConnectCancelTokenSource.Cancel();
                            }
                        }
                        else
                        {
                            rconReady = false;
                        }
                    }
                    if (rconReady)
                    {
                        _logger.LogInformation("RCON is connected. Setting up listener and Moving to AllReady");
                        waitForDemoFinishedTask = Task.Run(async () =>
                        {
                            var consoleFilePath = $"{_config["Game:GameDirPath"]}\\console.log";
                            await FileUtil.MonitorLogWaitForTarget(
                                consoleFilePath,
                                "Demo playback finished",
                                new FileInfo(consoleFilePath).Length);
                        });
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.AllReady);
                    }
                    else
                    {
                        // RconConnect seems to have failed
                        _logger.LogError("RconConnect appears to have failed.");
                        await StopAllModules();
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                        throw new Exception("RconConnect appears to have failed, please check logs.");
                    }
                    return;
                case DemoRecorderState.AllReady:
                    _logger.LogInformation("All modules are ready.");
                    if (recordingFinished == true)
                    {
                        // Do nothing and the awaiter in RecordDemo will reset the state.
                    }

                    if (recordingFinished == false)
                    {
                        // Move on to demo load.
                        _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.DemoLoad);
                    }
                    return;
                case DemoRecorderState.DemoLoad:
                    if (_currentDemoFileInfo != null)
                    {
                        // Send Demo Play Command to Game.
                        _logger.LogInformation("Sending demo play command to game.");
                        var playdemoResponse = _rconService.SendRconCommand($"playdemo \"{_currentDemoFileInfo.DemoPath}\"");
                        // Wait for response to rcon command OR timeout.
                        var whenAny = await Task.WhenAny(playdemoResponse, Task.Delay(60000));
                        if (whenAny == playdemoResponse)
                        {
                            // Check if play command was successful.
                            if (playdemoResponse.IsFaulted || playdemoResponse.IsCanceled)
                            {
                                _logger.LogError("Playdemo command failed or was cancelled.");

                            }
                            // Check if play command had right response.
                            else if (playdemoResponse.Result != null &&
                                     !playdemoResponse.Result.Contains(
                                         $"Playing demo from {_currentDemoFileInfo.DemoPath}"))
                            {
                                _logger.LogInformation("Demo play command failed.");
                                await StopAllModules();
                                _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                            }
                            else
                            {
                                // Keep echoing until a response is recieved (indicates app is responsive).
                                // Wait a few seconds when doing this, because the RCON will answer before loading actually starts.
                                await Task.Delay(5000);
                                var echoTask = _rconService.SendRconCommand("echo Demo Loaded!");
                                // If your demo file takes over 2 minutes to load... Am I out of touch? How many HDD-cels will use this?
                                if (await Task.WhenAny(echoTask, Task.Delay(120000)) == echoTask)
                                {
                                    _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.DemoReady);
                                }
                                else
                                {
                                    // Did not load before timeout
                                    _logger.LogError("Demo does not appear to have loaded in a timely fashion.");
                                    await StopAllModules();
                                    _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogError("Demo play command timed out.");
                            await StopAllModules();
                            _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
                            throw new Exception("Demo play command timed out, please check logs.");
                        }
                    }
                    return;
                case DemoRecorderState.DemoReady:
                    // Start Recording.
                    _logger.LogInformation("Demo play command successful. Starting recording.");
                    _obsController.Obs.StartRecord();
                    // Resume the demo
                    await _rconService.SendRconCommand("echo hold me");
                    _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Recording);
                    return;
                case DemoRecorderState.Recording:
                    await waitForDemoFinishedTask;
                    // Stop recording
                    _logger.LogInformation("Demo finished. Stopping recording.");
                    _obsController.Obs.StopRecord();
                    // Return to AllReady.
                    recordingFinished = true;
                    _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.AllReady);
                    return;
                default:
                    return;
            }
        }

        private async Task StopAllModules()
        {
            _logger.LogWarning("All modules stopping.");
            _gameLauncher.ExitGame();
            if (_obsController.Obs.IsConnected)
            {
                try
                {
                    _obsController.Obs.StopRecord();
                }
                catch{}
            }
            await _rconService.DisconnectRCON();
            _obsController.DisconnectObs();
        }

        // TODO: Handle RCON disconnect
        private void OnRconDisconnected(object sender, EventArgs e)
        {
            _logger.LogInformation("Demo Recorder: RCON Disconnected.");
        }

        // TODO: Handle OBS disconnect
        private void OnOBSDisconnected(object sender, EventArgs e)
        {
            _logger.LogInformation("Demo Recorder: OBS Disconnected.");
        }

        // TODO: Handle Game Close
        private void OnGameClosed(object sender, EventArgs e)
        {
            _logger.LogInformation("Demo Recorder: Game Closed.");
        }

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

            // No more demos, stop modules and go to idle.
            await StopAllModules();
            _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.Idle);
        }

        private async Task RecordDemo(DemoFileInfo demoFileInfo, CancellationToken cancellationToken)
        {
            _currentDemoFileInfo = demoFileInfo;
            _ = DemoRecorderStateChanged.Invoke(this, DemoRecorderState.ObsSetup);
            // Wait for recording to be finished
            await Task.Run(async () =>
            {
                while (recordingFinished == false)
                {
                    await Task.Delay(250);
                }
            });
            // Recording finished, reset flag.
            recordingFinished = false;
        }
    }

    public enum DemoRecorderState
    {
        Idle,
        GameSetup,
        RconSetup,
        ObsSetup,
        AllReady,
        DemoLoad,
        DemoReady,
        Recording
    }
}
