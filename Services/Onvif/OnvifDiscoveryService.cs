using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services.Onvif
{
    public class OnvifDiscoveryService
    {
        private const string MulticastAddress = "239.255.255.250";
        private const int MulticastPort = 3702;
        private static readonly XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace wsa = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
        private static readonly XNamespace wsd = "http://schemas.xmlsoap.org/ws/2005/04/discovery";

        public async Task<List<DiscoveredCamera>> DiscoverCamerasAsync(int timeoutMilliseconds = 3000)
        {
            var discoveredDevices = new List<DiscoveredCamera>();
            var endpoints = new HashSet<string>();
            var messageId = $"urn:uuid:{Guid.NewGuid()}";

            string probeMessage = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{soap.NamespaceName}"" xmlns:wsa=""{wsa.NamespaceName}"" xmlns:wsd=""{wsd.NamespaceName}"">
  <soap:Header>
    <wsa:Action soap:mustUnderstand=""1"">{wsd.NamespaceName}/Probe</wsa:Action>
    <wsa:MessageID>{messageId}</wsa:MessageID>
    <wsa:To soap:mustUnderstand=""1"">urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To>
  </soap:Header>
  <soap:Body>
    <wsd:Probe>
      <wsd:Types>wsd:NetworkVideoTransmitter</wsd:Types>
    </wsd:Probe>
  </soap:Body>
</soap:Envelope>";

            byte[] requestBytes = Encoding.UTF8.GetBytes(probeMessage);
            var localIps = GetLocalIpAddresses();

            using (var cts = new CancellationTokenSource(timeoutMilliseconds))
            {
                var tasks = new List<Task>();
                foreach (var ip in localIps)
                {
                    tasks.Add(Task.Run(() => SendAndReceiveDiscovery(ip, requestBytes, endpoints, discoveredDevices, cts.Token)));
                }

                await Task.WhenAll(tasks);
            }

            return discoveredDevices;
        }

        private static List<IPAddress> GetLocalIpAddresses()
        {
            var ips = new List<IPAddress>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ips.Add(addr.Address);
                        }
                    }
                }
            }
            if (ips.Count == 0)
            {
                ips.Add(IPAddress.Any);
            }
            return ips;
        }

        private void SendAndReceiveDiscovery(IPAddress localIp, byte[] requestBytes, HashSet<string> endpoints, List<DiscoveredCamera> devices, CancellationToken token)
        {
            UdpClient? udpClient = null;
            try
            {
                var localEndpoint = new IPEndPoint(localIp, 0);
                udpClient = new UdpClient(localEndpoint);
                udpClient.Client.ReceiveTimeout = 1000;
                
                // Multicast setup
                var multicastEp = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
                
                // Send probe
                udpClient.Send(requestBytes, requestBytes.Length, multicastEp);

                // Receive loop
                while (!token.IsCancellationRequested)
                {
                    if (udpClient.Available > 0)
                    {
                        var receiveTask = udpClient.ReceiveAsync();
                        if (receiveTask.Wait(1000, token))
                        {
                            var result = receiveTask.Result;
                            string responseXml = Encoding.UTF8.GetString(result.Buffer);
                            ParseProbeResponse(responseXml, endpoints, devices);
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery error on interface {localIp}: {ex.Message}");
            }
            finally
            {
                udpClient?.Close();
                udpClient?.Dispose();
            }
        }

        private void ParseProbeResponse(string xml, HashSet<string> endpoints, List<DiscoveredCamera> devices)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var namespaces = new XmlNamespaceManager(new NameTable());
                namespaces.AddNamespace("soap", soap.NamespaceName);
                namespaces.AddNamespace("wsa", wsa.NamespaceName);
                namespaces.AddNamespace("wsd", wsd.NamespaceName);

                // Find XAddrs element containing service URLs
                var xAddrsNode = doc.XPathSelectElement("//wsd:XAddrs", namespaces);
                if (xAddrsNode != null && !string.IsNullOrEmpty(xAddrsNode.Value))
                {
                    string[] urls = xAddrsNode.Value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var url in urls)
                    {
                        lock (endpoints)
                        {
                            if (endpoints.Contains(url))
                                continue;
                            endpoints.Add(url);
                        }

                        // Try parsing IP and port from url
                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                        {
                            string ip = uri.Host;
                            int port = uri.Port;

                            var cam = new DiscoveredCamera
                            {
                                IpAddress = ip,
                                OnvifPort = port,
                                ServiceUrl = url,
                                Manufacturer = "ONVIF",
                                Model = "Camera"
                            };

                            // Try to infer manufacturer/model from scopes if available
                            var scopesNode = doc.XPathSelectElement("//wsd:Scopes", namespaces);
                            if (scopesNode != null && !string.IsNullOrEmpty(scopesNode.Value))
                            {
                                string[] scopes = scopesNode.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var scope in scopes)
                                {
                                    // Scopes look like onvif://www.onvif.org/name/Hikvision or onvif://www.onvif.org/model/DS-2CD2121G0-I
                                    if (scope.Contains("/name/"))
                                    {
                                        cam.Manufacturer = Uri.UnescapeDataString(scope.Substring(scope.LastIndexOf('/') + 1));
                                    }
                                    else if (scope.Contains("/model/"))
                                    {
                                        cam.Model = Uri.UnescapeDataString(scope.Substring(scope.LastIndexOf('/') + 1));
                                    }
                                }
                            }

                            lock (devices)
                            {
                                if (!devices.Any(d => d.IpAddress == cam.IpAddress))
                                {
                                    devices.Add(cam);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse XML probe response: {ex.Message}");
            }
        }
    }
}
