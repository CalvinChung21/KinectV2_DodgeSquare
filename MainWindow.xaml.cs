// Kinect V2 API
using Microsoft.Kinect;
// .NET-based APIs from Microsoft
using System.Media;
using System.Windows;
// ColorFrameManager, DepthFrameManager and ImageHelper namespace
using NUI3D; 

// using the whole frame work of T5_BodilyInteraction
namespace DodgeSquare // <-- xaml namespace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor sensor = null;
        // depth frame
        DepthFrameManager depthFrameManager = null;
        // color frame
        ColorFrameManager colorFrameManger = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            System.Console.WriteLine("window loaded");
        }

        private void GameStartButton_Click(object sender, RoutedEventArgs e)
        {
            sensor = KinectSensor.GetDefault(); // get the default Kinect sensor 
            if (sensor == null) return;

            // depth stream 
            depthFrameManager = new DepthFrameManager();
            depthFrameManager.Init(sensor, depthImg, GameResultScore);

            // color stream 
            colorFrameManger = new ColorFrameManager();
            colorFrameManger.Init(sensor, colorImg);

            sensor.Open();
        }

        private void GameEndButton_Click(object sender, RoutedEventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}
