using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper.Models
{
    public class DemoEvent : IComparable<DemoEvent>
    {
        public Guid EventGuid { get; init; } = Guid.NewGuid();
        public int Tick { get; init; }
        public DemoEventType Type { get; init; }
        public string Value { get; init; }
        public string CustomName { get; set; }


        public int CompareTo(DemoEvent? other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            return Tick - other.Tick;
        }
        
    }
}
