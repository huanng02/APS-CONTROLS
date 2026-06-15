using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace QuanLyGiuXe.Services.Onvif
{
    public class OnvifDeviceClient
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public async Task<string> GetRtspUrlAsync(string deviceServiceUrl, string username, string password)
        {
            try
            {
                // 1. Get Capabilities (specifically Media service URL)
                string mediaServiceUrl = await GetMediaServiceUrlAsync(deviceServiceUrl, username, password);
                if (string.IsNullOrEmpty(mediaServiceUrl))
                {
                    // Fallback to deviceServiceUrl if media service url is not found (some cams use same url)
                    mediaServiceUrl = deviceServiceUrl;
                }

                // 2. Get Profiles
                string profileToken = await GetFirstProfileTokenAsync(mediaServiceUrl, username, password);
                if (string.IsNullOrEmpty(profileToken))
                {
                    throw new Exception("No ONVIF media profiles found.");
                }

                // 3. Get Stream URI
                string rtspUrl = await GetStreamUriAsync(mediaServiceUrl, profileToken, username, password);
                return rtspUrl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ONVIF error: {ex.Message}");
                throw;
            }
        }

        private async Task<string> GetMediaServiceUrlAsync(string deviceServiceUrl, string username, string password)
        {
            string wssHeader = GetWssHeader(username, password);
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tds=""http://www.onvif.org/ver10/device/wsdl"">
  {wssHeader}
  <s:Body>
    <tds:GetCapabilities>
      <tds:Category>Media</tds:Category>
    </tds:GetCapabilities>
  </s:Body>
</s:Envelope>";

            string responseXml = await PostSoapRequestAsync(deviceServiceUrl, soapEnvelope);
            var doc = XDocument.Parse(responseXml);
            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.AddNamespace("tt", "http://www.onvif.org/ver10/schema");

            var mediaNode = doc.XPathSelectElement("//tt:Media/tt:XAddr", ns);
            return mediaNode != null ? mediaNode.Value : string.Empty;
        }

        private async Task<string> GetFirstProfileTokenAsync(string mediaServiceUrl, string username, string password)
        {
            string wssHeader = GetWssHeader(username, password);
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"">
  {wssHeader}
  <s:Body>
    <trt:GetProfiles />
  </s:Body>
</s:Envelope>";

            string responseXml = await PostSoapRequestAsync(mediaServiceUrl, soapEnvelope);
            var doc = XDocument.Parse(responseXml);
            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.AddNamespace("trt", "http://www.onvif.org/ver10/media/wsdl");

            // Look for first profile element and its token attribute
            var profileNode = doc.XPathSelectElement("//trt:Profiles", ns);
            if (profileNode != null)
            {
                var tokenAttr = profileNode.Attribute("token");
                if (tokenAttr != null)
                {
                    return tokenAttr.Value;
                }
            }

            // Also try with standard media namespace schema just in case
            ns.AddNamespace("trt2", "http://www.onvif.org/ver20/media/wsdl");
            profileNode = doc.XPathSelectElement("//trt2:Profiles", ns);
            if (profileNode != null)
            {
                var tokenAttr = profileNode.Attribute("token");
                if (tokenAttr != null)
                {
                    return tokenAttr.Value;
                }
            }

            return string.Empty;
        }

        private async Task<string> GetStreamUriAsync(string mediaServiceUrl, string profileToken, string username, string password)
        {
            string wssHeader = GetWssHeader(username, password);
            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
  {wssHeader}
  <s:Body>
    <trt:GetStreamUri>
      <trt:StreamSetup>
        <tt:Stream>RTP-Unicast</tt:Stream>
        <tt:Transport>
          <tt:Protocol>RTSP</tt:Protocol>
        </tt:Transport>
      </trt:StreamSetup>
      <trt:ProfileToken>{profileToken}</trt:ProfileToken>
    </trt:GetStreamUri>
  </s:Body>
</s:Envelope>";

            string responseXml = await PostSoapRequestAsync(mediaServiceUrl, soapEnvelope);
            var doc = XDocument.Parse(responseXml);
            var ns = new XmlNamespaceManager(new NameTable());
            ns.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.AddNamespace("tt", "http://www.onvif.org/ver10/schema");

            var uriNode = doc.XPathSelectElement("//tt:Uri", ns);
            return uriNode != null ? uriNode.Value : string.Empty;
        }

        private async Task<string> PostSoapRequestAsync(string url, string envelope)
        {
            using (var content = new StringContent(envelope, Encoding.UTF8, "text/xml"))
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {response.StatusCode}: {errContent}");
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

        private string GetWssHeader(string username, string password)
        {
            if (string.IsNullOrEmpty(username)) return string.Empty;

            // Generate 16 random bytes for nonce
            byte[] nonceBytes = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            string nonceBase64 = Convert.ToBase64String(nonceBytes);

            string created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            byte[] createdBytes = Encoding.UTF8.GetBytes(created);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            // Hash buffer = Nonce bytes + Created bytes + Password bytes
            byte[] buffer = new byte[nonceBytes.Length + createdBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(nonceBytes, 0, buffer, 0, nonceBytes.Length);
            Buffer.BlockCopy(createdBytes, 0, buffer, nonceBytes.Length, createdBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, buffer, nonceBytes.Length + createdBytes.Length, passwordBytes.Length);

            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(buffer);
                string passwordDigest = Convert.ToBase64String(hash);

                return $@"<s:Header>
    <wsse:Security xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"" xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">
      <wsse:UsernameToken>
        <wsse:Username>{username}</wsse:Username>
        <wsse:Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"">{passwordDigest}</wsse:Password>
        <wsse:Nonce EncodingType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"">{nonceBase64}</wsse:Nonce>
        <wsu:Created>{created}</wsu:Created>
      </wsse:UsernameToken>
    </wsse:Security>
  </s:Header>";
            }
        }
    }
}
