using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public enum LicenseStatus
    {
        NotActivated,
        Activated,
        Invalid,
        HardwareMismatch,
        Expired,
        Revoked,
        SignatureInvalid,
        Corrupted,
        FeatureNotLicensed
    }

    public class OfflineLicense
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("product")]
        public string Product { get; set; } = "APS";

        [JsonProperty("customer")]
        public string Customer { get; set; } = string.Empty;

        [JsonProperty("machineId")]
        public string MachineId { get; set; } = string.Empty;

        [JsonProperty("licenseType")]
        public string LicenseType { get; set; } = "Commercial";

        [JsonProperty("features")]
        public List<string> Features { get; set; } = new();

        [JsonProperty("hardware")]
        public HardwareDetails Hardware { get; set; } = new();

        [JsonProperty("createdDate")]
        public string CreatedDate { get; set; } = string.Empty;

        [JsonProperty("expirationDate")]
        public string ExpirationDate { get; set; } = string.Empty;

        [JsonProperty("signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonProperty("lastTimeUsedEncrypted")]
        public string LastTimeUsedEncrypted { get; set; } = string.Empty;
    }

    public static class LicenseManager
    {
        public static LicenseStatus CurrentStatus { get; set; } = LicenseStatus.NotActivated;
        public static OfflineLicense? CurrentLicense { get; set; }

        public static bool HasFeature(string featureName)
        {
            if (CurrentStatus != LicenseStatus.Activated || CurrentLicense == null)
            {
                return false;
            }

            if (CurrentLicense.Features == null)
            {
                return false;
            }

            return CurrentLicense.Features.Contains(featureName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
