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
    public class OBSController
    {
        private readonly ILogger<OBSController> _logger;
        private readonly OBSWebsocket obs;
        public OBSConnectionSettings connectionSettings { get; private set; }

        public OBSController(ILogger<OBSController> logger)
        {
            _logger = logger;
            obs = new OBSWebsocket();

            obs.Connected += onConnect;
            obs.Disconnected += onDisconnect;
        }

        public void setObsConnectionSettings(OBSConnectionSettings connectionSettings)
        {
            if (obs.IsConnected)
            {
                throw new InvalidOperationException("The OBS Connection settings cannot be altered while OBS is connected. Please disconnect from OBS first.");
            }
            else 
            {
                this.connectionSettings = connectionSettings;
            }
            
        }

        private void onConnect(object sender, EventArgs e)
        {
            _logger.LogInformation($"Connected to OBS @ {connectionSettings.Host}.");

            var streamStatus = obs.GetStreamingStatus();

            if (streamStatus.IsRecording)
                onRecordingStateChange(obs, OutputState.Started);
            else
                onRecordingStateChange(obs, OutputState.Stopped);
        }
        private void onDisconnect(object sender, EventArgs e)
        {
            _logger.LogInformation($"Disconnected from OBS.");
        }

        private void onRecordingStateChange(OBSWebsocket sender, OutputState newState)
        {
            switch (newState)
            {
                case OutputState.Starting:
                    break;

                case OutputState.Started:
                    break;

                case OutputState.Stopping:
                    break;

                case OutputState.Stopped:
                    break;

                default:
                    break;
            }
        }
    }
}
