using LibTF2AutoClipper.Models;
using Newtonsoft.Json;
using System.Text;

namespace LibTF2AutoClipper
{

    public class FileUtil
    {
        /// <summary>
        /// Returns a list of all files in a directory with a given extension.
        /// </summary>
        /// <param name="Extension">File extension to check filenames for.</param>
        /// <param name="DirectoryPath">The path to the directory to search.</param>
        /// <param name="Recursive">Search directory and all sub-directories.</param>
        /// <returns>A List with the paths of the files that were found.</returns>
        public static List<string> ListAllFilesWithExtensionInDirectoryPath(string Extension, string DirectoryPath, bool Recursive = false)
        {
            if (string.IsNullOrEmpty(Extension))
            {
                throw new ArgumentException($"'{nameof(Extension)}' cannot be null or empty.", nameof(Extension));
            }

            if (string.IsNullOrEmpty(DirectoryPath))
            {
                throw new ArgumentException($"'{nameof(DirectoryPath)}' cannot be null or empty.", nameof(DirectoryPath));
            }

            return Directory.EnumerateFiles(DirectoryPath, $"*", Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
        }
        
        public static DemoFileInfo CreateDemoFileInfoFromDemoPath(string path, string? tfEventFilePath = null, string clipperEventFilePath = null)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }

            // Convert TFEvents to DemoEvents
            TFEventFile? tfEventFile = null;
            List<DemoEvent>? tfDemoEvents = null;
            if (tfEventFilePath == null) {
                tfEventFilePath = Path.ChangeExtension(path, ".json");
            }
            if (File.Exists(tfEventFilePath))
            {
                tfEventFile = JsonConvert.DeserializeObject<TFEventFile>(File.ReadAllText(tfEventFilePath));
                tfDemoEvents = new List<DemoEvent>();
                foreach (TFEventFileEvent tfEvent in tfEventFile.Events)
                {
                    tfDemoEvents.Add(new DemoEvent
                    {
                        Tick = tfEvent.Tick,
                        Type = tfEvent.Name.ToDemoEventType(),
                        Value = tfEvent.Value
                    });
                }
            }

            DemoFileInfo demoFileInfo = new DemoFileInfo
            {
                DemoPath = path,
                TFDemoEvents = tfDemoEvents != null && tfDemoEvents.Count > 0 ? tfDemoEvents : null,
                EventFilePath = tfEventFilePath
            };

            return demoFileInfo;
        }

        public static List<DemoFileInfo> CreateDemoFileInfoList(string DirectoryPath, bool Recursive = false)
        {
            List<DemoFileInfo> demoFileInfoList = new List<DemoFileInfo>();
            foreach (string demoPath in ListAllFilesWithExtensionInDirectoryPath(".dem", DirectoryPath, Recursive))
            {
                demoFileInfoList.Add(CreateDemoFileInfoFromDemoPath(demoPath));
            }
            return demoFileInfoList;
        }

        public static async Task MonitorLogWaitForTarget(string filename, string target, long startBytes)
        {
            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                try
                {
                    while (true)
                    {
                        // Seek 1024 bytes from the end of the file,
                        // AFTER startBytes
                        var adjustedOffset = Math.Max(fs.Length - 1024, startBytes) == startBytes ? 0 : fs.Length - startBytes;
                        if (adjustedOffset > 1024) adjustedOffset = 1024;
                        if (adjustedOffset > 0)
                        {
                            fs.Seek(-adjustedOffset, SeekOrigin.End);
                            // read 1024 bytes
                            byte[] bytes = new byte[adjustedOffset];
                            fs.Read(bytes, 0, bytes.Length);
                            // Convert bytes to string
                            string s = Encoding.UTF8.GetString(bytes);
                            if (!s.Contains(target))
                            {
                                await Task.Delay(1000);
                            }
                            else break;
                        }
                        else
                        {
                            await Task.Delay(1000);
                        }
                    }
                }
                finally
                {
                    fs.Close();
                }
            }
        }
    }
}