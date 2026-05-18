using QuanLyGiuXe.Services;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

public class CameraData
{
    public string CamKey { get; set; }
    public Bitmap Frame { get; set; }
    public BitmapSource FrameForUI { get; set; }
}
public class CameraService : IDisposable
{
    private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();

    public event EventHandler<CameraData> NewFrameReceived;

    public void StartIpCamera(string camKey, string url)
    {
        StopIpCamera(camKey);

        var cts = new CancellationTokenSource();
        _ipCameraTokens[camKey] = cts;

        Task.Run(() =>
        {
            using var capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG);
            if (!capture.IsOpened()) return;

            using var mat = new Mat();
            while (!cts.Token.IsCancellationRequested)
            {
                if (capture.Read(mat) && !mat.Empty())
                {
                    using (Bitmap bitmap = BitmapConverter.ToBitmap(mat))
                    {
                        var uiSource = ConvertToBitmapSource(bitmap);

                        var data = new CameraData
                        {
                            CamKey = camKey,
                            Frame = (Bitmap)bitmap.Clone(),
                            FrameForUI = uiSource
                        };
                        NewFrameReceived?.Invoke(this, data);
                    }
                }
                Thread.Sleep(30);
            }
        }, cts.Token);
    }

    private static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
    {
        if (bitmap == null) return null;
        try
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                96, 96, // DPI chuẩn
                PixelFormats.Bgr24,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch { return null; }
    }

    public void StopIpCamera(string camKey)
    {
        if (_ipCameraTokens.TryGetValue(camKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _ipCameraTokens.Remove(camKey);
        }
    }

    public void Dispose()
    {
        foreach (var token in _ipCameraTokens.Values)
        {
            token.Cancel();
            token.Dispose();
        }
        _ipCameraTokens.Clear();
    }
}