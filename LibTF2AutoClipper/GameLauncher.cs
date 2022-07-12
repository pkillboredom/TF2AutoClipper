using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper
{
    public delegate void GameLauncherStateCallback(GameLauncher sender, GameLauncherState state);
    public class GameLauncher
    {
        private readonly string appBasePath = AppDomain.CurrentDomain.BaseDirectory;
        private readonly ILogger<GameLauncher> _logger;
        public GameLauncherState GameLauncherState { get; private set; }
        public event GameLauncherStateCallback GameLauncherStateChanged;
        private Process GameProcess; 
        private string? CurrentGameDirPath;

        public GameLauncher(ILogger<GameLauncher> logger)
        {
            _logger = logger;
            GameLauncherState = GameLauncherState.NotLaunched;
        }
        
        public async Task LaunchGame(string gameExePath, string gameDirPath, string args, CancellationToken cancellationToken)
        {
            CurrentGameDirPath = gameDirPath;
            try
            {
                DoStateChange(GameLauncherState.Configuring);
                ReplaceUserCfgAndCustomWithClipperFiles(gameDirPath);
                DoStateChange(GameLauncherState.Launching);
                GameProcess = Process.Start(gameExePath, args);
                GameProcess.Exited += OnGameProcessExited;
                // Wait for RCON connection.
                // Make RCON connection accessible at GameLauncher.RCON?
                // On RCON Success set status Launched.
                // On RCON fail set status error, kill Process.
            }
            catch
            {
                RestoreUserCfgAndCustom(gameDirPath);
                CurrentGameDirPath = null;
                DoStateChange(GameLauncherState.Error);
            }
        }

        private void OnGameProcessExited(Object sender, EventArgs e)
        {
            // Clean Up
            if (CurrentGameDirPath != null) { 
                RestoreUserCfgAndCustom(CurrentGameDirPath); 
            }
            if (GameProcess.ExitCode != 0)
            {
                DoStateChange(GameLauncherState.Error);
            }
            else
            {
                DoStateChange(GameLauncherState.Exited);
            }
        }

        private void DoStateChange(GameLauncherState state)
        {
            GameLauncherState = state;
            GameLauncherStateChanged?.Invoke(this, state);
        }

        private void RenameCfgAndCustom(string gameDirectoryPath)
        {
            string cfgFolderPath = Path.Join(gameDirectoryPath, "cfg");
            string customFolderPath = Path.Join(gameDirectoryPath, "custom");

            bool cfgExists = Directory.Exists(cfgFolderPath);
            bool customExists = Directory.Exists(customFolderPath);
            if (!cfgExists)
            {
                throw new FileNotFoundException("The cfg folder could not be found, is the game directory correctly set to the '/tf' folder?");
            }
            else if (cfgExists && !customExists)
            {
                Directory.CreateDirectory(customFolderPath);
            }

            string cfgBackupPath = Path.Join(gameDirectoryPath, "cfg_user_backup");
            string customBackupPath = Path.Join(gameDirectoryPath, "custom_user_backup");
            // if the backups already exist (unclean shutdown?) rename them with a guid so they are not lost.
            if (Directory.Exists(cfgBackupPath))
            {
                Directory.Move(cfgBackupPath, cfgBackupPath + Guid.NewGuid().ToString("N"));
            }
            if (Directory.Exists(customBackupPath))
            {
                Directory.Move(customBackupPath, customBackupPath + Guid.NewGuid().ToString("N"));
            }
            Directory.Move(cfgFolderPath, cfgBackupPath);
            Directory.Move(customFolderPath, customBackupPath);
        }

        private void RestoreCfgAndCustom(string gameDirectoryPath)
        {
            string cfgFolderPath = Path.Join(gameDirectoryPath, "cfg_user_backup");
            string customFolderPath = Path.Join(gameDirectoryPath, "custom_user_backup");


            if (Directory.Exists(cfgFolderPath))
            {
                Directory.Move(cfgFolderPath, Path.Join(gameDirectoryPath, "cfg"));
            }
            else _logger.LogWarning($"Directory '{cfgFolderPath}' did not exist and was not restored");
            if (Directory.Exists(customFolderPath))
            {
                Directory.Move(customFolderPath, Path.Join(gameDirectoryPath, "custom"));
            }
            else _logger.LogWarning($"Directory '{customFolderPath}' did not exist and was not restored");
        }

        private void SymLinkClipperCfgAndCustom(string gameDirectoryPath)
        {
            string cfgFolderPath = Path.Join(gameDirectoryPath, "cfg");
            if (Directory.Exists(cfgFolderPath)) throw new Exception("cfg folder still exists in /tf, cant create symlink.");
            string customFolderPath = Path.Join(gameDirectoryPath, "custom");
            if (Directory.Exists(customFolderPath)) throw new Exception("custom folder still exists in /tf, cant create symlink.");
            string autoClipperTFDirectoryPath = Path.Join(appBasePath, "tf-files");
            string autoClipperCfgDirectoryPath = Path.Join(autoClipperTFDirectoryPath, "cfg");
            string autoClipperCustomDirectoryPath = Path.Join(autoClipperTFDirectoryPath, "custom");
            Directory.CreateSymbolicLink(cfgFolderPath, autoClipperCfgDirectoryPath);
            Directory.CreateSymbolicLink(customFolderPath, autoClipperCustomDirectoryPath);
        }

        private void RemoveClipperSymLinks(string gameDirectoryPath)
        {
            string cfgFolderPath = Path.Join(gameDirectoryPath, "cfg");
            string customFolderPath = Path.Join(gameDirectoryPath, "custom");
            if (Directory.Exists(cfgFolderPath)) Directory.Delete(cfgFolderPath);
            if (Directory.Exists(customFolderPath)) Directory.Delete(customFolderPath);
        }

        public void ReplaceUserCfgAndCustomWithClipperFiles(string gameDirectoryPath)
        {
            try
            {
                RenameCfgAndCustom(gameDirectoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception renaming/backing-up user's cfg and custom folders. Attempting to restore.");
                try
                {
                    RestoreCfgAndCustom(gameDirectoryPath);
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "There was an exception restoring cfg and custom folders after an exception renaming them.");
                    throw new AggregateException(new Exception[] {ex, ex2});
                }
                throw;
            }
            try
            {
                SymLinkClipperCfgAndCustom(gameDirectoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception creating a symlink to the Clipper application's cfg and custom folders.");
                throw;
            }
        }

        public void RestoreUserCfgAndCustom(string gameDirectoryPath)
        {
            try
            {
                RemoveClipperSymLinks(gameDirectoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception deleting the symlink to the Clipper application's cfg and custom folders.");
                throw;
            }
            try
            {
                RestoreCfgAndCustom(gameDirectoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception restoring users's cfg and custom folders from backup.");
                throw;
            }
        }
    }

    public enum GameLauncherState
    {
        NotLaunched,
        Configuring,
        Launching,
        Launched,
        Exited,
        Error
    }
}
