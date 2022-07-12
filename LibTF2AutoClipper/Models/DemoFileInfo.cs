using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper.Models
{
    public class DemoFileInfo
    {
        public string DemoFileId { get; init; }
        public string DemoPath { get; init; }
        public string? EventFilePath { get; init; } = null;
        public List<DemoEvent>? TFDemoEvents { get; init; } = null;
        public List<DemoEvent>? ClipperDemoEvents { get; init; } = null;
        public int Length { get; init; }
        public List<DemoEvent>? CombinedDemoEvents
        {
            get
            {
                if (TFDemoEvents != null && ClipperDemoEvents != null)
                {
                    return (List<DemoEvent>)TFDemoEvents.Zip(ClipperDemoEvents);
                }
                if (TFDemoEvents != null)
                {
                    return TFDemoEvents;
                }
                if (ClipperDemoEvents != null)
                {
                    return ClipperDemoEvents;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
