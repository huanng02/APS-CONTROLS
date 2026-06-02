using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class ControllerCapabilityRegistry
    {
        private static readonly Dictionary<ControllerType, ControllerCapability> _capabilities = new()
        {
            {
                ControllerType.C3200,
                new ControllerCapability
                {
                    Type = ControllerType.C3200,
                    ReaderCount = 4,
                    RelayCount = 2,
                    MaxSupportedLanes = 2
                }
            },
            {
                ControllerType.C3400,
                new ControllerCapability
                {
                    Type = ControllerType.C3400,
                    ReaderCount = 4,
                    RelayCount = 4,
                    MaxSupportedLanes = 4
                }
            }
        };

        public static ControllerCapability GetCapability(ControllerType type)
        {
            if (_capabilities.TryGetValue(type, out var capability))
            {
                return capability;
            }
            // Fallback default C3200
            return _capabilities[ControllerType.C3200];
        }

        public static IEnumerable<ControllerCapability> GetAllCapabilities()
        {
            return _capabilities.Values;
        }
    }
}
