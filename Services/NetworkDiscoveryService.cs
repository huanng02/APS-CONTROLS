using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;

namespace QuanLyGiuXe.Services
{
    public class CameraDetected
    {
        public string IP { get; set; }
        public string MAC { get; set; }
        public string Vendor { get; set; }
        public string RtspUrl { get; set; }
    }

    public class NetworkDiscoveryService
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref uint physicalAddrLen);

        public async Task<List<CameraDetected>> ScanNetworkAsync(string baseIpPrefix = "192.168.2.")
        {
            var detectedList = new List<CameraDetected>();
            var tasks = new List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                string ip = baseIpPrefix + i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Đặt cấu hình thời gian chờ tối đa cho mỗi IP là 200ms
                        string mac = GetMacAddress(ip);

                        if (!string.IsNullOrEmpty(mac) && mac.Length >= 8)
                        {
                            string url = "rtsp://" + ip + "/live"; // Gán tạm trước để tránh crash hàm GetRtspUrl
                            try { url = GetRtspUrl(ip, mac); } catch { }

                            string vendorName = "Generic";
                            try { vendorName = GetVendorName(mac); } catch { }

                            lock (detectedList)
                            {
                                detectedList.Add(new CameraDetected
                                {
                                    IP = ip,
                                    MAC = mac,
                                    Vendor = vendorName,
                                    RtspUrl = url
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Có lỗi ở 1 IP đơn lẻ thì bỏ qua, không làm treo toàn hệ thống
                    }
                }));
            }

            // Khống chế tổng thời gian quét mạng tối đa là 5 giây, quá 5s tự nhả giao diện
            var completionTask = Task.WhenAll(tasks);
            var timeoutTask = Task.Delay(5000);

            await Task.WhenAny(completionTask, timeoutTask);

            return detectedList.OrderBy(x => {
                try
                {
                    var parts = x.IP.Split('.');
                    return int.Parse(parts.Last());
                }
                catch { return 0; }
            }).ToList();
        }

        private string GetMacAddress(string ipAddress)
        {
            try
            {
                IPAddress dst = IPAddress.Parse(ipAddress);
                byte[] macAddr = new byte[6];
                uint len = (uint)macAddr.Length;

#pragma warning disable CS0618 // Tắt cảnh báo Obsolete của .NET 8 để chạy nhanh
                int destIp = (int)dst.Address;
#pragma warning restore CS0618

                int res = SendARP(destIp, 0, macAddr, ref len);

                return res == 0 ? BitConverter.ToString(macAddr).Replace("-", ":") : null;
            }
            catch { return null; }
        }

        private string GetVendorName(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return "Unknown";
            string oui = mac.Length >= 8 ? mac.Substring(0, 8).ToUpper() : "";

            return oui switch
            {
                "00:12:41" or "A4:B1:C1" or "A4:E8:8D" => "Enster/XMeye",
                "00:40:8C" or "BC:AD:28" => "Hikvision",
                "3C:EF:8C" or "BC:32:5F" => "Dahua",
                _ => "Generic"
            };
        }

        public string GetRtspUrl(string ip, string mac)
        {
            string oui = (mac ?? "").Length >= 8 ? mac.Substring(0, 8).ToUpper() : "";

            return oui switch
            {
                "00:12:41" or "A4:B1:C1" or "A4:E8:8D" => $"rtsp://admin:tlJwpbo6@{ip}:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp",
                "00:40:8C" or "BC:AD:28" => $"rtsp://admin:12345@{ip}/Streaming/Channels/101",
                "3C:EF:8C" or "BC:32:5F" => $"rtsp://admin:admin123@{ip}/cam/realmonitor?channel=1&subtype=0",
                _ => $"rtsp://admin:12345@{ip}/live"
            };
        }
        public string GetCurrentIpPrefix()
        {
            try
            {
                // Lấy tên máy và danh sách các IP của máy hiện tại
                string hostName = System.Net.Dns.GetHostName();
                var ipEntry = System.Net.Dns.GetHostEntry(hostName);

                // Lọc ra IP thuộc dạng IPv4 (InterNetwork) và không phải IP ảo (loopback)
                var ip = ipEntry.AddressList.FirstOrDefault(a =>
                    a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !a.ToString().StartsWith("127."));

                if (ip != null)
                {
                    string ipStr = ip.ToString(); // Ví dụ: "192.168.2.9"
                                                  // Cắt chuỗi lấy đến dấu chấm cuối cùng để thành "192.168.2."
                    return ipStr.Substring(0, ipStr.LastIndexOf('.') + 1);
                }
            }
            catch { }
            return "192.168.1."; // Dự phòng nếu lỗi thì quay về mặc định
        }
    }
}