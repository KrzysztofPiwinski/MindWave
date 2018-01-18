using Accord.Video.FFMPEG;
using AForge.Video;
using AForge.Video.DirectShow;
using libStreamSDK;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WpfApplication2
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set { _currentDevice = value; this.OnPropertyChanged("CurrentDevice"); }
        }
        public BitmapImage Image
        {
            get { return _image; }
            set { _image = value; this.OnPropertyChanged("Image"); }
        }

        private BitmapImage _image;
        private FilterInfo _currentDevice;
        private IVideoSource _videoSource;
        private VideoFileWriter _writer;
        private bool _recording;
        private DateTime? _firstFrameTime;
        private int connectionID;

        public MainWindow()
        {
            InitializeComponent();
            NativeThinkgear thinkgear = new NativeThinkgear();
            this.DataContext = this;
            FindAllVideoDevices();
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            StopCamera();
            StopRecording();
        }

        private void FindAllVideoDevices()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            foreach (FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                VideoDevices.Add(filterInfo);
            }

            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("Brak urządzeń", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_recording)
            {
                StartCamera();
                StartMindWaveConnection();
                StartRecording();
            }
        }

        private void StartMindWaveConnection()
        {
            var a = NativeThinkgear.TG_GetVersion();
            connectionID = NativeThinkgear.TG_GetNewConnectionId();

            int errCode;

            for (int i = 1; i < 10; i++)
            {
                errCode = NativeThinkgear.TG_Connect(connectionID,
                              "\\\\.\\COM" + i,
                              NativeThinkgear.Baudrate.TG_BAUD_57600,
                              NativeThinkgear.SerialDataFormat.TG_STREAM_PACKETS);

                if (errCode == 0)
                {
                    break;
                }

            }

            errCode = NativeThinkgear.TG_ReadPackets(connectionID, 1);
            errCode = NativeThinkgear.TG_EnableAutoRead(connectionID, 1);
        }

        private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += video_NewFrame;
                _videoSource.Start();
            }
            else
            {
                MessageBox.Show("Brak kamery.");
            }
        }

        public void StartRecording()
        {
            try
            {
                var dialog = new SaveFileDialog();
                dialog.FileName = "Video1";
                dialog.DefaultExt = ".avi";
                dialog.AddExtension = true;
                var dialogResult = dialog.ShowDialog();
                if (dialogResult != true)
                {
                    return;
                }

                _firstFrameTime = null;
                _writer = new VideoFileWriter();
                _writer.Open(dialog.FileName, (int)Math.Round(Image.Width, 0), (int)Math.Round(Image.Height, 0), 25, VideoCodec.MPEG4);
                _recording = true;

            }
            catch (Exception ex)
            {
                File.WriteAllText("file.txt", ex.ToString());
            }
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap b = (Bitmap)eventArgs.Frame.Clone();
            PointF firstLocation = new PointF(10f, 25f);
            PointF secondLocation = new PointF(10f, 50f);

            using (Graphics graphics = Graphics.FromImage(b))
            {
                using (Font arialFont = new Font("Arial", 24))
                {
                    float value = NativeThinkgear.TG_GetValue(connectionID, NativeThinkgear.DataType.TG_DATA_ATTENTION);
                    graphics.DrawString("Stopień skupienia: " + value, arialFont, System.Drawing.Brushes.Blue, firstLocation);

                    float value2 = NativeThinkgear.TG_GetValue(connectionID, NativeThinkgear.DataType.TG_DATA_MEDITATION);
                    graphics.DrawString("Stopień relaksu: " + value2, arialFont, System.Drawing.Brushes.Blue, secondLocation);
                }
            }

            if (_recording)
            {
                if (_firstFrameTime != null)
                {
                    _writer.WriteVideoFrame(b, DateTime.Now - _firstFrameTime.Value);
                }
                else
                {
                    _writer.WriteVideoFrame(b);
                    _firstFrameTime = DateTime.Now;
                }
            }
            using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
            {
                Image = b.ToBitmapImage();
            }

            Image.Freeze();
            Dispatcher.BeginInvoke(new ThreadStart(delegate { video.Source = Image; }));

            StopCamera();
            StopRecording();
            StopMindWaveConnection();

        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
            StopRecording();
            StopMindWaveConnection();
        }

        private void StopCamera()
        {
            if (_videoSource != null)
            {
                _videoSource.Stop();
                _videoSource.NewFrame -= video_NewFrame;
            }

            Image = null;
            Dispatcher.BeginInvoke(new ThreadStart(delegate { video.Source = Image; }));
        }

        private void StopRecording()
        {
            _recording = false;
            StopMindWaveConnection();
            _writer.Close();
            _writer.Dispose();
        }

        private void StopMindWaveConnection()
        {
            NativeThinkgear.TG_Disconnect(connectionID);
            NativeThinkgear.TG_FreeConnection(connectionID);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;

            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

    }
}
