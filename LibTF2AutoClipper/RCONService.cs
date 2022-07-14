using CoreRCON;
using LibTF2AutoClipper.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper
{
    public class RCONService
    {
        private static SemaphoreSlim _connectingSemaphore = new SemaphoreSlim(1,1);
        private const string checkString = "TF2AutoClipper has connected to RCON.";

        private readonly ILogger<RCONService> _logger;
        private RCON? _rcon;

        public ConnectionSettings? ConnectionSettings { get; private set; }
        public event EventHandler? RCONConnected;
        public event EventHandler? RCONDisconnected;
        public RconStatus ConnectionStatus { get; private set; }

        public RCONService(ILogger<RCONService> logger)
        {
            _logger = logger;
            RCONConnected += OnRconConnect;
            RCONDisconnected += OnRconDisconnect;
        }

        public async Task ConnectRCON(ConnectionSettings connectionSettings)
        {
            int timeout = 30000;
            var waitToConnect = _connectingSemaphore.WaitAsync();
            try
            {
                if (await Task.WhenAny(waitToConnect, Task.Delay(timeout)) == waitToConnect)
                {
                    if (ConnectionStatus == RconStatus.Connected || ConnectionStatus == RconStatus.Connecting)
                    {
                        throw new InvalidOperationException("The current RCON connection must be disconnected before a new one can be established.");
                    }
                    else
                    {
                        ConnectionStatus = RconStatus.Connecting;
                        ConnectionSettings = connectionSettings;
                        _rcon = new RCON(IPAddress.Parse(connectionSettings.Host), (ushort)connectionSettings.Port, connectionSettings.Password);
                        var rconConnect = _rcon.ConnectAsync();
                        if (await Task.WhenAny(rconConnect, Task.Delay(timeout)) == rconConnect)
                        {
                            _rcon.OnDisconnected += RconCoreDisconnected;
                            RCONConnected?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            _rcon.Dispose();
                            _rcon = null;
                            ConnectionSettings = null;
                            throw new TimeoutException("Time out while establishing RCON connection.");
                        }
                    }
                }
                else
                {
                    throw new TimeoutException("Could not get a lock on the connecting semaphore. Another caller is already connecting or disconnecting.");
                }
            }
            finally
            {
                _connectingSemaphore.Release();
            }
        }

        public async Task DisconnectRCON()
        {
            _logger.LogInformation("Attempting disconnect from RCON");
            int timeout = 10000;
            var waitToDisconnect = _connectingSemaphore.WaitAsync();
            try
            {
                if (await Task.WhenAny(waitToDisconnect, Task.Delay(timeout)) == waitToDisconnect)
                {
                    if (ConnectionStatus != RconStatus.Disconnected)
                    {
                        if (_rcon != null) _rcon.Dispose();
                        _rcon = null;
                        ConnectionSettings = null;
                        ConnectionStatus = RconStatus.Disconnected;
                        RCONDisconnected?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        throw new InvalidOperationException("The current RCON connection must be connected before it can be disconnected.");
                    }
                }
                else
                {
                    throw new TimeoutException("Could not get a lock on the connecting semaphore. Another caller is already connecting or disconnecting.");
                }
            }
            finally
            {
                _connectingSemaphore.Release();
            }
        }

        private async void OnRconConnect(object sender, EventArgs args)
        {
            if (ConnectionSettings == null || _rcon == null) {
                _logger.LogCritical("Fatal! OnRconConnect event triggered with null _connectionSettings or _rcon!");
                throw new InvalidOperationException("Fatal! OnRconConnect event triggered with null _connectionSettings or _rcon!");
            }
            else {
                _logger.LogInformation($"RCON connected to {ConnectionSettings.Host} on port {ConnectionSettings.Port}.");
                int timeout = 2000;
                string checkString = "TF2AutoClipper has connected to RCON.";
                var echoResponseTask = _rcon.SendCommandAsync($"echo {checkString}");
                if (await Task.WhenAny(echoResponseTask, Task.Delay(timeout)) == echoResponseTask)
                {
                    if (echoResponseTask.Result == checkString)
                    {
                        ConnectionStatus = RconStatus.Connected;
                        return;
                    }
                    else
                    {
                        _logger.LogError($"RCON echo response did not match expected response: {echoResponseTask.Result}");
                        _ = DisconnectRCON();
                        throw new InvalidOperationException("RCON echo response did not match expected response.");
                    }
                }
            }
        }

        private void OnRconDisconnect(object sender, EventArgs args)
        {
            _logger.LogInformation("RCON was disconnected.");
            ConnectionStatus = RconStatus.Disconnected;
        }

        internal void RconCoreDisconnected()
        {
            RCONDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public enum RconStatus
    {
        Disconnected,
        Connected,
        Connecting,
    }
}
