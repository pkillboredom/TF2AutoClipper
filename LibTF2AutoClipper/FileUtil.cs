using LibTF2AutoClipper.Models;
using Newtonsoft.Json;

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
        public List<string> ListAllFilesWithExtensionInDirectoryPath(string Extension, string DirectoryPath, bool Recursive = false)
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
        
        public DemoFileInfo CreateDemoFileInfoFromDemoPath(string path, string? tfEventFilePath = null, string clipperEventFilePath = null)
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

        public List<DemoFileInfo> CreateDemoFileInfoList(string DirectoryPath, bool Recursive = false)
        {
            List<DemoFileInfo> demoFileInfoList = new List<DemoFileInfo>();
            foreach (string demoPath in ListAllFilesWithExtensionInDirectoryPath(".dem", DirectoryPath, Recursive))
            {
                demoFileInfoList.Add(CreateDemoFileInfoFromDemoPath(demoPath));
            }
            return demoFileInfoList;
        }
    }
}