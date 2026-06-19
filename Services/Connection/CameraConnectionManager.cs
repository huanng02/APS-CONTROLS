using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using AForge.Video.DirectShow;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services.Connection
{
    public class CameraConnection : IDisposable
    {
        public string Url { get; }
        public VideoCapture? Capture { get; set; }
        public Mat LatestFrame { get; } = new Mat();
        
        public CancellationTokenSource? Cts { get; set; }
        public Task? CaptureTask { get; set; }
        
        public HashSet<string> Consumers { get; } = new();
        
        // Connection health & metrics
        public bool IsConnected { get; set; }
        public int ReconnectCount { get; set; }
        public DateTime LastHeartbeat { get; set; } = DateTime.MinValue;
        public DateTime LastFrameReceived { get; set; } = DateTime.MinValue;
        public double CurrentFps { get; set; }
        
        public int FrameCount { get; set; }
        public DateTime LastStatTime { get; set; } = DateTime.UtcNow;

        public CameraConnection(string url)
        {
            Url = url;
        }

        public void Dispose()
        {
            if (LatestFrame != null)
            {
                lock (LatestFrame)
                {
                    LatestFrame.Dispose();
                }
            }
        }
    }

    public class CameraConnectionManager
    {
        private static readonly Lazy<CameraConnectionManager> _instance = 
            new Lazy<CameraConnectionManager>(() => new CameraConnectionManager());
            
        public static CameraConnectionManager Instance => _instance.Value;
        
        private readonly ConcurrentDictionary<string, CameraConnection> _connections = new();
        private readonly object _managerLock = new();
        
        public event EventHandler<(string CamKey, Mat Frame)>? FrameReceived;

        private CameraConnectionManager() {}

        public string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                string normalized = url.ToLower().Trim();
                normalized = normalized.Replace("_password=", "password=")
                                       .Replace("_username=", "username=")
                                       .Replace("&password=", "")
                                       .Replace("password=", "")
                                       .Replace("&username=", "")
                                       .Replace("username=", "");
                return normalized;
            }
            catch
            {
                return url;
            }
        }
        
        public IReadOnlyDictionary<string, CameraConnection> GetConnections() => _connections;
        
        public void StartStream(string consumerKey, string url, int targetWidth, int targetHeight, int maxFps)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                var config = AppConfig.Load();
                if (config?.Cameras?.AutoUseSubstream ?? true)
                {
                    url = ConvertToSubstreamUrl(url);
                }
            }
            catch { }

            string normalizedUrl = NormalizeUrl(url);
            
            lock (_managerLock)
            {
                if (!_connections.TryGetValue(normalizedUrl, out var conn))
                {
                    conn = new CameraConnection(url);
                    _connections[normalizedUrl] = conn;
                    
                    conn.Cts = new CancellationTokenSource();
                    var token = conn.Cts.Token;
                    conn.Consumers.Add(consumerKey);
                    
                    conn.CaptureTask = Task.Run(() => CaptureLoopAsync(conn, targetWidth, targetHeight, maxFps, token), token);
                    try { LoggingService.Instance.LogInfo("ConnManager", "StartStream", $"Created new RTSP stream for {normalizedUrl} (First consumer: {consumerKey})"); } catch {}
                }
                else
                {
                    if (!conn.Consumers.Contains(consumerKey))
                      {
                          conn.Consumers.Add(consumerKey);
                          try { LoggingService.Instance.LogInfo("ConnManager", "StartStream", $"Reused active RTSP stream for {normalizedUrl} (Added consumer: {consumerKey}, total: {conn.Consumers.Count})"); } catch {}
                      }
                }
            }
        }
        
        public void StopStream(string consumerKey)
        {
            lock (_managerLock)
            {
                string? targetUrl = null;
                CameraConnection? targetConn = null;
                
                foreach (var kvp in _connections)
                {
                    if (kvp.Value.Consumers.Contains(consumerKey))
                    {
                        targetUrl = kvp.Key;
                        targetConn = kvp.Value;
                        break;
                    }
                }
                
                if (targetConn != null && targetUrl != null)
                {
                    targetConn.Consumers.Remove(consumerKey);
                    try { LoggingService.Instance.LogInfo("ConnManager", "StopStream", $"Removed consumer: {consumerKey} from RTSP stream {targetUrl} (Remaining: {targetConn.Consumers.Count})"); } catch {}
                    
                    if (targetConn.Consumers.Count == 0)
                    {
                        DisposeConnection(targetUrl, targetConn);
                    }
                }
            }
        }
        
        public void StopAll()
        {
            lock (_managerLock)
            {
                foreach (var kvp in _connections)
                {
                    DisposeConnection(kvp.Key, kvp.Value);
                }
                _connections.Clear();
            }
        }
        
        private void DisposeConnection(string urlKey, CameraConnection conn)
        {
            try { LoggingService.Instance.LogInfo("ConnManager", "DisposeConnection", $"Shutting down RTSP stream for {urlKey} (No consumers left)"); } catch {}
            
            _connections.TryRemove(urlKey, out _);
            conn.Cts?.Cancel();
        }
        
        public Mat? GetLatestFrame(string consumerKey)
        {
            foreach (var conn in _connections.Values)
            {
                if (conn.Consumers.Contains(consumerKey))
                {
                    if (conn.LatestFrame != null)
                    {
                        lock (conn.LatestFrame)
                        {
                            if (!conn.LatestFrame.Empty())
                            {
                                return conn.LatestFrame.Clone();
                            }
                        }
                    }
                }
            }
            return null;
        }
        
        public CameraConnection? GetConnectionByKey(string consumerKey)
        {
            foreach (var conn in _connections.Values)
            {
                if (conn.Consumers.Contains(consumerKey))
                {
                    return conn;
                }
            }
            return null;
        }

        private async Task CaptureLoopAsync(CameraConnection conn, int targetWidth, int targetHeight, int maxFps, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        VideoCapture? cap = null;
                        if (conn.Url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                            conn.Url.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                            conn.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            conn.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            cap = new VideoCapture(conn.Url, VideoCaptureAPIs.FFMPEG);
                        }
                        else if (int.TryParse(conn.Url, out int index))
                        {
                            cap = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                        }
                        else
                        {
                            int foundIndex = -1;
                            try
                            {
                                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                                for (int i = 0; i < devices.Count; i++)
                                {
                                    if (devices[i].Name.Equals(conn.Url, StringComparison.OrdinalIgnoreCase) ||
                                        devices[i].Name.Contains(conn.Url, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foundIndex = i;
                                        break;
                                    }
                                }
                            }
                            catch {}

                            if (foundIndex >= 0)
                            {
                                cap = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                            }
                            else
                            {
                                cap = new VideoCapture(conn.Url);
                            }
                        }

                        if (cap == null || !cap.IsOpened())
                        {
                            cap?.Dispose();
                            conn.IsConnected = false;
                            await Task.Delay(5000, token);
                            conn.ReconnectCount++;
                            continue;
                        }

                        conn.Capture = cap;
                        using (cap)
                        {
                            cap.Set((VideoCaptureProperties)38, 1);
                            conn.IsConnected = true;
                            conn.LastHeartbeat = DateTime.UtcNow;

                            using var tempMat = new Mat();
                            int failCount = 0;
                            DateTime lastFrameTime = DateTime.MinValue;

                            while (!token.IsCancellationRequested)
                            {
                                bool readSuccess = false;
                                try
                                {
                                    readSuccess = cap.Read(tempMat);
                                }
                                catch
                                {
                                    readSuccess = false;
                                }

                                if (readSuccess && !tempMat.Empty())
                                {
                                    failCount = 0;
                                    conn.IsConnected = true;
                                    conn.LastHeartbeat = DateTime.UtcNow;
                                    conn.LastFrameReceived = DateTime.UtcNow;
                                    
                                    conn.FrameCount++;
                                    var now = DateTime.UtcNow;
                                    double elapsedStat = (now - conn.LastStatTime).TotalSeconds;
                                    if (elapsedStat >= 5.0)
                                    {
                                        conn.CurrentFps = conn.FrameCount / elapsedStat;
                                        conn.FrameCount = 0;
                                        conn.LastStatTime = now;
                                    }

                                    double elapsedMs = (now - lastFrameTime).TotalMilliseconds;
                                    double targetIntervalMs = 1000.0 / maxFps;

                                    if (elapsedMs >= targetIntervalMs)
                                    {
                                        lastFrameTime = now;
                                        
                                        if (conn.LatestFrame != null)
                                        {
                                            lock (conn.LatestFrame)
                                            {
                                                if (targetWidth > 0 && targetHeight > 0 && 
                                                    (tempMat.Width != targetWidth || tempMat.Height != targetHeight))
                                                {
                                                    Cv2.Resize(tempMat, conn.LatestFrame, new OpenCvSharp.Size(targetWidth, targetHeight));
                                                }
                                                else
                                                {
                                                    tempMat.CopyTo(conn.LatestFrame);
                                                }
                                            }
                                        }

                                        System.Collections.Generic.List<string> consumersCopy;
                                        lock (_managerLock)
                                        {
                                            consumersCopy = new System.Collections.Generic.List<string>(conn.Consumers);
                                        }

                                        foreach (var consumerKey in consumersCopy)
                                        {
                                            FrameReceived?.Invoke(this, (consumerKey, conn.LatestFrame));
                                        }
                                    }
                                }
                                else
                                {
                                    var now = DateTime.UtcNow;
                                    var timeSinceLastFrame = now - conn.LastFrameReceived;
                                    var timeSinceStart = now - conn.LastHeartbeat;
                                    
                                    bool timeout = (conn.LastFrameReceived == DateTime.MinValue)
                                        ? (timeSinceStart.TotalSeconds > 8.0)
                                        : (timeSinceLastFrame.TotalSeconds > 8.0);
                                        
                                    if (timeout)
                                    {
                                        conn.IsConnected = false;
                                        try { LoggingService.Instance.LogWarning("ConnManager", "CaptureLoop", $"Stream timeout for {conn.Url} (No frames received for 8s). Reconnecting..."); } catch {}
                                        break;
                                    }
                                }

                                await Task.Delay(5, token);
                            }
                        }
                        
                        conn.Capture = null;

                        if (!token.IsCancellationRequested)
                        {
                            await Task.Delay(5000, token);
                            conn.ReconnectCount++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        conn.IsConnected = false;
                        if (!token.IsCancellationRequested)
                        {
                            await Task.Delay(5000, token);
                            conn.ReconnectCount++;
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    conn.Capture?.Dispose();
                    conn.Dispose();
                    conn.Cts?.Dispose();
                }
                catch {}
            }
        }

        public static string ConvertToSubstreamUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            // Dahua / KBVision: subtype=0 -> subtype=1
            if (url.Contains("subtype=0"))
            {
                return url.Replace("subtype=0", "subtype=1");
            }

            // Hikvision / Ezviz / Imou: /main/ -> /sub/
            if (url.Contains("/main/"))
            {
                return url.Replace("/main/", "/sub/");
            }

            // Hikvision / Ezviz: /Channels/101 -> /Channels/102
            if (url.Contains("/Channels/101"))
            {
                return url.Replace("/Channels/101", "/Channels/102");
            }
            if (url.Contains("/channels/101"))
            {
                return url.Replace("/channels/101", "/channels/102");
            }

            // Other cameras (e.g. channel=0_stream=0 -> channel=0_stream=1)
            if (url.Contains("stream=0"))
            {
                return System.Text.RegularExpressions.Regex.Replace(url, @"stream=0(?!\d)", "stream=1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return url;
        }
    }
}
