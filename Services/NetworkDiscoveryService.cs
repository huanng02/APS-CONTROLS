using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public class CameraDetected
    {
        public string IP { get; set; }
        public string MAC { get; set; }
        public string Vendor { get; set; }
        public string RtspUrl { get; set; }
    }

    public class CameraBrandConfig
    {
        public string Brand { get; set; }
        public string PasswordDefault { get; set; }
        public string RtspTemplate { get; set; }
        public List<string> MacPrefixes { get; set; }
    }

    public class NetworkDiscoveryService
    {
        private List<CameraBrandConfig> _brandsConfig = new List<CameraBrandConfig>();

        public NetworkDiscoveryService()
        {
            LoadBrandsFromJson();
        }

        private void LoadBrandsFromJson()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "camera_brands.json");
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _brandsConfig = JsonSerializer.Deserialize<List<CameraBrandConfig>>(jsonContent, options) ?? new List<CameraBrandConfig>();
                }
            }
            catch
            {
                _brandsConfig = new List<CameraBrandConfig>();
            }
        }

        /// <summary>
        /// Quét và định danh chính xác tất cả Camera IP bằng giao thức ONVIF (WS-Discovery)
        /// Bỏ qua hoàn toàn giới hạn dải IP mạng LAN
        /// </summary>
        public async Task<List<CameraDetected>> ScanNetworkAsync(string unusedParameter = "")
        {
            var detectedList = new List<CameraDetected>();
            var discoveredIps = new HashSet<string>(); // Tránh trùng lặp IP

            // Chuỗi Probe chuẩn của liên minh ONVIF để gọi tất cả Camera IP thức dậy phản hồi
            string messageId = Guid.NewGuid().ToString();
            string soapProbe =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Envelope xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\" xmlns:dn=\"http://www.onvif.org/ver10/network/wsdl\" xmlns=\"http://www.w3.org/2003/05/soap-envelope\">" +
                "  <Header>" +
                "    <MessageID xmlns=\"http://www.w3.org/2005/08/addressing\">urn:uuid:" + messageId + "</MessageID>" +
                "    <To xmlns=\"http://www.w3.org/2005/08/addressing\">urn:schemas-xmlsoap-org:node:ephemeral:id</To>" +
                "    <Action xmlns=\"http://www.w3.org/2005/08/addressing\">http://schemas.xmlsoap-org.xml/ws/2005/04/discovery/Probe</Action>" +
                "  </Header>" +
                "  <Body>" +
                "    <Probe xmlns=\"http://schemas.xmlsoap-org.xml/ws/2005/04/discovery\">" +
                "      <Types>tds:Device dn:NetworkVideoTransmitter</Types>" +
                "    </Probe>" +
                "  </Body>" +
                "</Envelope>";

            byte[] requestBytes = Encoding.UTF8.GetBytes(soapProbe);

            // Cổng và địa chỉ Multicast chuẩn của giao thức ONVIF toàn cầu
            var multicastEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702);

            using (var udpClient = new UdpClient())
            {
                try
                {
                    udpClient.EnableBroadcast = true;
                    udpClient.Client.ReceiveTimeout = 2000; // Chờ camera phản hồi trong 2 giây

                    // Bắn gói tin ONVIF ra toàn mạng LAN
                    await udpClient.SendAsync(requestBytes, requestBytes.Length, multicastEndpoint);

                    // Lắng nghe tất cả camera phản hồi ngược lại
                    var listenTask = Task.Run(() =>
                    {
                        while (true)
                        {
                            try
                            {
                                IPEndPoint remoteEP = null;
                                byte[] responseBytes = udpClient.Receive(ref remoteEP);
                                string responseString = Encoding.UTF8.GetString(responseBytes);

                                // Trích xuất địa chỉ IP thực tế từ nội dung XML phản hồi của Camera
                                string ip = ExtractIpFromXml(responseString);
                                if (string.IsNullOrEmpty(ip) || discoveredIps.Contains(ip)) continue;

                                discoveredIps.Add(ip);

                                // Định danh hãng và gán luồng dựa vào Port đặc trưng hoặc thông tin XML
                                var (vendorName, rtspUrl) = DinhDanhQuaThongTinOnvif(ip, responseString);

                                lock (detectedList)
                                {
                                    detectedList.Add(new CameraDetected
                                    {
                                        IP = ip,
                                        MAC = "ONVIF Device", // Giao thức ONVIF trả về thẳng Service URL, không cần MAC
                                        Vendor = vendorName,
                                        RtspUrl = rtspUrl
                                    });
                                }
                            }
                            catch (SocketException)
                            {
                                break; // Hết thời gian chờ (Timeout), kết thúc việc nhận dữ liệu
                            }
                            catch { }
                        }
                    });

                    // Khống chế thời gian chờ tổng thể là 2.5 giây để đóng cổng nhận hình
                    await Task.WhenAny(listenTask, Task.Delay(2500));
                }
                catch { }
            }

            // Sắp xếp danh sách IP tăng dần đều để kỹ thuật dễ nhìn
            return detectedList.OrderBy(x => {
                try { return int.Parse(x.IP.Split('.').Last()); } catch { return 0; }
            }).ToList();
        }

        // Hàm dùng Regex bốc tách địa chỉ IP nằm giữa các thẻ URL trong XML của Camera
        private string ExtractIpFromXml(string xml)
        {
            var match = Regex.Match(xml, @"http://(?<ip>[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})");
            return match.Success ? match.Groups["ip"].Value : null;
        }

        // Hàm định danh thông minh: phối hợp Port dịch vụ mở và File JSON cấu hình ngoài
        private (string Vendor, string RtspUrl) DinhDanhQuaThongTinOnvif(string ip, string xml)
        {
            string vendor = "Generic ONVIF";
            string passDefault = "12345";
            string rtspTemplate = "rtsp://{user}:{pass}@{ip}:554/live";

            // Dùng thuật toán kiểm tra nhanh cổng dịch vụ mở (Port Scan) để nhận diện hãng
            if (xml.Contains("hardware/XM") || KiemTraPortMo(ip, 34567)) // Cổng 34567 độc quyền của chip Xiongmai (Enster)
            {
                var ensterCfg = _brandsConfig.FirstOrDefault(b => b.Brand.Contains("Enster"));
                vendor = "Enster/XMeye";
                passDefault = ensterCfg != null ? ensterCfg.PasswordDefault : "tlJwpbo6";
                rtspTemplate = ensterCfg != null ? ensterCfg.RtspTemplate : "rtsp://{user}:{pass}@{ip}:554/user={user}&password={pass}&channel=0&stream=0.sdp";
            }
            else if (xml.ToLower().Contains("hikvision") || KiemTraPortMo(ip, 8000))
            {
                var hikCfg = _brandsConfig.FirstOrDefault(b => b.Brand.Contains("Hikvision"));
                vendor = "Hikvision";
                passDefault = hikCfg != null ? hikCfg.PasswordDefault : "12345";
                rtspTemplate = hikCfg != null ? hikCfg.RtspTemplate : "rtsp://{user}:{pass}@{ip}/Streaming/Channels/101";
            }
            else if (xml.ToLower().Contains("dahua") || KiemTraPortMo(ip, 37777))
            {
                var dahuaCfg = _brandsConfig.FirstOrDefault(b => b.Brand.Contains("Dahua"));
                vendor = "Dahua/Imou";
                passDefault = dahuaCfg != null ? dahuaCfg.PasswordDefault : "admin123";
                rtspTemplate = dahuaCfg != null ? dahuaCfg.RtspTemplate : "rtsp://{user}:{pass}@{ip}/cam/realmonitor?channel=1&subtype=0";
            }

            string realRtsp = rtspTemplate
                .Replace("{user}", "admin")
                .Replace("{pass}", passDefault)
                .Replace("{ip}", ip);

            return (vendor, realRtsp);
        }

        private bool KiemTraPortMo(string ip, int port)
        {
            using (var tcpClient = new TcpClient())
            {
                try
                {
                    var result = tcpClient.ConnectAsync(ip, port);
                    Task.WaitAny(new Task[] { result }, 150);
                    return tcpClient.Connected;
                }
                catch { return false; }
            }
        }

        public string GetCurrentIpPrefix() => "ONVIF_MODE";
    }
}