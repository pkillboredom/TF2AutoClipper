using LibTF2AutoClipper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper
{
    public interface IObsController
    {
        OBSWebsocket Obs { get; }
        ConnectionSettings ConnectionSettings { get; }
        void SetObsConnectionSettings(ConnectionSettings connectionSettings);
        void ConnectObs(ConnectionSettings connectionSettings);
        void ConnectObs();
        void DisconnectObs();
    }

    public class ObsController : IObsController
    {
        private readonly ILogger<ObsController> _logger;
        public OBSWebsocket Obs { get; private set; }
        public ConnectionSettings ConnectionSettings { get; private set; }

        public ObsController(ILogger<ObsController> logger)
        {
            _logger = logger;
            Obs = new OBSWebsocket();
            
            Obs.Connected += OnConnect;
            Obs.Disconnected += OnDisconnect;
        }

        public void SetObsConnectionSettings(ConnectionSettings connectionSettings)
        {
            if (Obs.IsConnected)
            {
                throw new InvalidOperationException("The OBS Connection settings cannot be altered while OBS is connected. Please disconnect from OBS first.");
            }
            else 
            {
                this.ConnectionSettings = connectionSettings;
            }
            
        }

        public void ConnectObs(ConnectionSettings connectionSettings)
        {
            SetObsConnectionSettings(connectionSettings);
            var host = ConnectionSettings.Host;
            host = $"ws://{ConnectionSettings.Host}:{ConnectionSettings.Port}";
            Obs.Connect(host, ConnectionSettings.Password);
        }

        public void ConnectObs()
        {
            var host = ConnectionSettings.Host;
            host = $"ws://{ConnectionSettings.Host}:{ConnectionSettings.Port}";
            Obs.Connect(host, ConnectionSettings.Password);
        }

        public void DisconnectObs()
        {
            Obs.Disconnect();
        }

        private void OnConnect(object sender, EventArgs e)
        {
            _logger.LogInformation($"Connected to OBS @ {ConnectionSettings.Host}.");

            //var streamStatus = obs.GetStreamingStatus();

            //if (streamStatus.IsRecording)
            //    onRecordingStateChange(obs, OutputState.Started);
            //else
            //    onRecordingStateChange(obs, OutputState.Stopped);
        }

        private void OnDisconnect(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            _logger.LogInformation($"Disconnected from OBS: {e.ObsCloseCode}, {e.DisconnectReason}.");
        }

        //private void OnRecordingStateChange(OBSWebsocket sender, OutputState newState)
        //{
        //    switch (newState)
        //    {
        //        case OutputState.Starting:
        //            break;

        //        case OutputState.Started:
        //            break;

        //        case OutputState.Stopping:
        //            break;

        //        case OutputState.Stopped:
        //            break;

        //        default:
        //            break;
        //    }
        //}
    }
}
