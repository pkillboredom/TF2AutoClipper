using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibTF2AutoClipper.Models
{
    public record ConnectionSettings
    {
        public string? Host { get; init; }
        public int Port { get; init; }
        public string? Password { get; init; }
    }
}
