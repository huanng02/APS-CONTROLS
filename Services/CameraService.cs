using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using AForge.Video;
using AForge.Video.DirectShow;

namespace QuanLyGiuXe.Services
{
    public class CameraService
    {
        public static CameraService Instance = new CameraService();

        private FilterInfoCollection cameras;
        private VideoCaptureDevice cam;

        private Bitmap _currentFrame;
        private readonly object _lock = new object();

        public event Action<Bitmap> OnFrameUpdated;

        public void Start()
        {
            cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (cameras.Count == 0)
                throw new Exception("Không tìm thấy camera");

            foreach (FilterInfo camera in cameras)
            {
                if (camera.Name.ToLower().Contains("ivcam"))
                {
                    cam = new VideoCaptureDevice(camera.MonikerString);
                    break;
                }
            }

            if (cam == null)
                cam = new VideoCaptureDevice(cameras[0].MonikerString);

            cam.NewFrame += Cam_NewFrame;
            cam.Start();
        }

        private void Cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();

            lock (_lock)
            {
                _currentFrame = (Bitmap)frame.Clone();
            }

            OnFrameUpdated?.Invoke(frame);
        }

        public Bitmap GetCurrentFrame()
        {
            lock (_lock)
            {
                if (_currentFrame == null)
                    return null;

                return (Bitmap)_currentFrame.Clone();
            }
        }

        public string CaptureAndSave(string uid, string type = "vao")
        {
            Bitmap frame = GetCurrentFrame();

            if (frame == null)
                return null;

            string folder = $@"D:\APS\ParkingImages\{type}\";

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = $"{uid}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string fullPath = Path.Combine(folder, fileName);

            frame.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);

            return fullPath;
        }

        public void Stop()
        {
            if (cam != null && cam.IsRunning)
            {
                cam.SignalToStop();
                cam.WaitForStop();
            }
        }
    }
}
