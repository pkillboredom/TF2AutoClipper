using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LibTF2AutoClipper.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DemoEventType
    {
        Kill,
        Killstreak,
        Survival
    }

    internal static class DemoEventTypeExtensions
    {
        public static DemoEventType ToDemoEventType(this string str)
        {
            switch (str)
            {
                case "Kill":
                    return DemoEventType.Kill;
                case "Killstreak":
                    return DemoEventType.Killstreak;
                case "Survival":
                    return DemoEventType.Survival;
                default:
                    throw new ArgumentException($"The supplied string '{str}' was not a valid DemoEventType.");
            }
        }
    }
}
