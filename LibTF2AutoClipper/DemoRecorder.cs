using LibTF2AutoClipper.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper
{
    public class DemoRecorder
    {
        private readonly ILogger<DemoRecorder> _logger;

        public DemoRecorder(ILogger<DemoRecorder> logger)
        {
            _logger = logger;
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
        }

        private async Task RecordDemo(DemoFileInfo demoFileInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
