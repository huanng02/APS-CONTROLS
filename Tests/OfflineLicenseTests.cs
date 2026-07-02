using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Tests
{
    public static class OfflineLicenseTests
    {
        private const string PrivateKeyXml = @"<RSAKeyValue><Modulus>ojILooC76YJpMh60RLTCKsgoxexHbG0fKZ0qt1TV32MEu3dDh8DWnuoVL8m6ZcJrS2GOG9837mqc/G435R2mO/+WwgvBysK93kZzrjZZ4iVBaRQ6VXsTeYd+Aj8WawQTkOhDujKC77qYMs2DETbsdp8GGcyf02NTzgV4C43SsYD2CTKdD5IgJ2okUBOIAq2G8lsqS294w6J0hLuelmfOLPd53mHZPX0ufnipU7jwJQp38zljvL5Sjp4QvrjJc3EWAVrunC3hR1LKZs7BLUbjdZWCyLSiBBaSsErW8pSU6KSEC9vjsgfrsCG5bpIvI8WkLelHmyc4uVFprb5InR5J9Q==</Modulus><Exponent>AQAB</Exponent><P>1m2OoV3dRn76zYN/ljGYVaIwQGh8+irMAgQJMqR5RIoFFUThFR6zyQTUNvWp18/o/5qb4AUA93mdDIO5Hay1+EemuwVnH3Sd/qxh4OvLaV0ppHvGGTN8Aqe84XQ3GHieAlsGFTzIuBshv8+fceB78RFt7lB+SH8vban6y4kmGgM=</P><Q>waQY+cNj3d8LTYWk4FXbUR05SI1KOQonOFGrOGILMJYUjXM7gjKPC8oBGdc2niu5ZmKySvryXAGotiP5qQR4W0/0dKai3CWEFySmmzkT8llinIRREkYQMCjt2fdTd6AMOsr0nCSK3qWX5Av37T/P9bvk3xhXe5S6HGcI1Ae3xqc=</Q><DP>vLiP39YM+g6oDli94iKkQDoO3aEY3dTs2JlUvw2i7X/MGXwV3dC3yyRE4lo0sYx7NPuOVQwSXbzbTDhipIttXKczR0bqC/VHWO2+94aP8JveGrYVE/kMHAolYwg1tYPzDX+vSuHEhsTaX0cMvd0lOHZummCdxJCr3YjNAnYi4qE=</DP><DQ>rANCcHRCPXCKENY8LU/3X+nO3gUsvtinGF9r8s0dVY6sOS742OJisb1DFxpXmVAMBMh9yx96tYJ/xTTV7W9cHvk6lXkFSPxGh2x2V4LvliQS9iiP/+SfMrjY+Pu8eJKC6qMpgZ7wgXGmKNz84xMBgC/l0sxDwjLO1LYuYHNurBc=</DQ><InverseQ>GozJPBzLJ4hRGaINY3BwgTbd2gn1Ltf9+nXVfvie5jIYEfmzlE05yjhnr1XbRfziPAe1/rqNHdV1SoQFvYLtGPVn7xk82a5jmbwkPHad/YaT25bheaJ3ypilZmHTNI3mF+K7cJyqr48YCm1+yXDRIDGoZe2/O0UgtR/kEfvykaQ=</InverseQ><D>Ree36AP/+XaBjF57Z5lYjkPSfuuFJRAq/C6G+JkRzMPKiFmwu1O7rKZLF1ukgLM4tzaGnzCn1JQSsSF36cHLodRYz61tisxANQq8VPuL5dIUzQsw0SLIk/p3rtQt/1W0cSIJ/rhCgrwzWMIGmWbIp5+Ga5wrzlnjBsqIoMIxatrwTv6UiHhS7Mu2y+HB8RBStuI5nMd3ozWWqCo8aOmdudPrJM14yi2j9fU69HNcw3fdNdZ5j7UWFupaTcYDbmKDlDxWOP3fwbNaQatAmYy1jvqYZbSoILHe/GzvMFZ7GIBQCkfy/aGbccu0cLK08KqJmpWAeStuUNPiJpS0w1kHiQ==</D></RSAKeyValue>";

        public static void Run()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("RUNNING OFFLINE LICENSE & HARDWARE BINDING TESTS");
            Console.WriteLine("=================================================");

            TestHardwareFingerprintService();
            TestHardwareMatchScore();
            TestOfflineLicenseSignatureVerification();
            TestFeatureControl();

            Console.WriteLine(">> ALL OFFLINE LICENSE TESTS PASSED SUCCESSFULLY!");
            Console.WriteLine("=================================================");
        }

        private static void TestHardwareFingerprintService()
        {
            Console.WriteLine("Running: TestHardwareFingerprintService...");

            var details = HardwareFingerprintService.GetHardwareDetails();
            Assert(!string.IsNullOrEmpty(details.Cpu), "CPU ID should not be empty");
            Assert(!string.IsNullOrEmpty(details.Motherboard), "Motherboard Serial should not be empty");
            Assert(!string.IsNullOrEmpty(details.Disk), "Disk Serial should not be empty");
            Assert(!string.IsNullOrEmpty(details.Bios), "BIOS Serial should not be empty");

            string machineId = HardwareFingerprintService.CalculateMachineId(details);
            Assert(machineId.Length == 12, $"MachineId length should be 12 (actual: {machineId.Length})");
            
            // Check that Machine ID is uppercase hex
            foreach (char c in machineId)
            {
                Assert((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'), "MachineId characters must be uppercase Hex");
            }
        }

        private static void TestHardwareMatchScore()
        {
            Console.WriteLine("Running: TestHardwareMatchScore...");

            var local = new HardwareDetails
            {
                Cpu = "CPU123",
                Motherboard = "MOBO456",
                Disk = "DISK789",
                Bios = "BIOS012"
            };

            // 1. Perfect Match (100%)
            var match100 = new HardwareDetails
            {
                Cpu = "CPU123",
                Motherboard = "MOBO456",
                Disk = "DISK789",
                Bios = "BIOS012"
            };
            double score100 = LicenseValidationService.Instance.CalculateHardwareMatchScore(local, match100);
            Assert(score100 == 100, $"Perfect match should be 100% (actual: {score100}%)");

            // 2. Disk Replaced (85% match - valid)
            var diskReplaced = new HardwareDetails
            {
                Cpu = "CPU123",
                Motherboard = "MOBO456",
                Disk = "NEW_DISK_999",
                Bios = "BIOS012"
            };
            double scoreDisk = LicenseValidationService.Instance.CalculateHardwareMatchScore(local, diskReplaced);
            Assert(scoreDisk == 85, $"Disk replaced score should be 85% (actual: {scoreDisk}%)");

            // 3. CPU and Disk Replaced (70% match - valid)
            var cpuDiskReplaced = new HardwareDetails
            {
                Cpu = "NEW_CPU_999",
                Motherboard = "MOBO456",
                Disk = "NEW_DISK_999",
                Bios = "BIOS012"
            };
            double scoreCpuDisk = LicenseValidationService.Instance.CalculateHardwareMatchScore(local, cpuDiskReplaced);
            Assert(scoreCpuDisk == 70, $"CPU and Disk replaced score should be 70% (actual: {scoreCpuDisk}%)");

            // 4. Motherboard Replaced (50% match - invalid)
            var moboReplaced = new HardwareDetails
            {
                Cpu = "CPU123",
                Motherboard = "NEW_MOBO_999",
                Disk = "DISK789",
                Bios = "BIOS012"
            };
            double scoreMobo = LicenseValidationService.Instance.CalculateHardwareMatchScore(local, moboReplaced);
            Assert(scoreMobo == 50, $"Mobo replaced score should be 50% (actual: {scoreMobo}%)");
        }

        private static void TestOfflineLicenseSignatureVerification()
        {
            Console.WriteLine("Running: TestOfflineLicenseSignatureVerification...");

            var license = new OfflineLicense
            {
                Version = 1,
                Product = "APS",
                Customer = "Test Customer",
                MachineId = "A1B2C3D4E5F6",
                LicenseType = "Commercial",
                Features = new List<string> { "LPR", "RFID", "CAMERA", "REPORT" },
                Hardware = new HardwareDetails
                {
                    Cpu = "CPU_TEST",
                    Motherboard = "MB_TEST",
                    Disk = "DISK_TEST",
                    Bios = "BIOS_TEST"
                },
                CreatedDate = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ExpirationDate = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // Sign the payload using the private key
            var sortedFeatures = license.Features.OrderBy(f => f).ToList();
            var payload = $"{license.Version}|{license.Product}|{license.Customer}|{license.MachineId}|{license.LicenseType}|{string.Join(",", sortedFeatures)}|{license.Hardware.Cpu}|{license.Hardware.Motherboard}|{license.Hardware.Disk}|{license.Hardware.Bios}|{license.CreatedDate}|{license.ExpirationDate}";
            
            string signature;
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(PrivateKeyXml);
                var dataBytes = Encoding.UTF8.GetBytes(payload);
                var sigBytes = rsa.SignData(dataBytes, CryptoConfig.MapNameToOID("SHA256")!);
                signature = Convert.ToBase64String(sigBytes);
            }

            license.Signature = signature;

            // Verify with local Hardware Details matching
            // We simulate local matching by setting the license hardware to the local machine details
            var localDetails = HardwareFingerprintService.GetHardwareDetails();
            license.Hardware = localDetails;
            license.MachineId = HardwareFingerprintService.CalculateMachineId(localDetails);

            // Re-sign with matching hardware details
            var sortedFeaturesMatch = license.Features.OrderBy(f => f).ToList();
            var payloadMatch = $"{license.Version}|{license.Product}|{license.Customer}|{license.MachineId}|{license.LicenseType}|{string.Join(",", sortedFeaturesMatch)}|{license.Hardware.Cpu}|{license.Hardware.Motherboard}|{license.Hardware.Disk}|{license.Hardware.Bios}|{license.CreatedDate}|{license.ExpirationDate}";
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(PrivateKeyXml);
                var dataBytes = Encoding.UTF8.GetBytes(payloadMatch);
                var sigBytes = rsa.SignData(dataBytes, CryptoConfig.MapNameToOID("SHA256")!);
                license.Signature = Convert.ToBase64String(sigBytes);
            }

            string errorMsg;
            bool success = LicenseValidationService.Instance.VerifyOfflineLicense(license, out errorMsg);
            Assert(success, $"VerifyOfflineLicense failed: {errorMsg}");
            Assert(LicenseManager.CurrentStatus == LicenseStatus.Activated, "Status should be Activated");

            // Tamper check (change customer name)
            license.Customer = "Tampered Customer Name";
            bool tamperedSuccess = LicenseValidationService.Instance.VerifyOfflineLicense(license, out errorMsg);
            Assert(!tamperedSuccess, "Tampered license should not verify");
            Assert(LicenseManager.CurrentStatus == LicenseStatus.SignatureInvalid, "Tampered status should be SignatureInvalid");
        }

        private static void TestFeatureControl()
        {
            Console.WriteLine("Running: TestFeatureControl...");

            LicenseManager.CurrentStatus = LicenseStatus.Activated;
            LicenseManager.CurrentLicense = new OfflineLicense
            {
                Features = new List<string> { "LPR", "RFID" }
            };

            Assert(LicenseManager.HasFeature("LPR"), "LPR should be licensed");
            Assert(LicenseManager.HasFeature("RFID"), "RFID should be licensed");
            Assert(!LicenseManager.HasFeature("REPORT"), "REPORT should not be licensed");
            Assert(!LicenseManager.HasFeature("CAMERA"), "CAMERA should not be licensed");

            // Deactivate and check
            LicenseManager.CurrentStatus = LicenseStatus.Expired;
            Assert(!LicenseManager.HasFeature("LPR"), "Feature check should return false when status is not Activated");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
