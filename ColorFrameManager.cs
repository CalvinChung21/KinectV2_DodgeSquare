// Kinect V2 API
using Microsoft.Kinect;
// .NET-based APIs from Microsoft
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;

// using the whole frame work of T5_BodilyInteraction
// the purpose of this script is to display the color image captured from Kinect on the WPF image source
namespace NUI3D
{
    public class ColorFrameManager
    {
        private KinectSensor sensor;
        // receive color data from Kinect
        private FrameDescription colorFrameDescription = null;
        private byte[] colorData = null;
        // write color data on the bitmap
        private WriteableBitmap colorImageBitmap = null;

        public void Init(KinectSensor s, Image wpfImageForDisplay)
        {
            sensor = s;
            ColorFrameReaderInit(wpfImageForDisplay);
        }

        private void ColorFrameReaderInit(Image wpfImageForDisplay)
        {
            // Open the reader for the color frames
            ColorFrameReader colorFrameReader = sensor.ColorFrameSource.OpenReader();

            // register an event handler for FrameArrived 
            colorFrameReader.FrameArrived += ColorFrameReader_FrameArrived;
            // init a frame description that has the properties of the Kinect's color frame
            colorFrameDescription = sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            // intermediate storage for receiving frame data from the sensor 
            // allowcate enough storage to store the color data
            // using the formular : total length in pixels times the bytes per pixel
            colorData = new byte[colorFrameDescription.LengthInPixels * colorFrameDescription.BytesPerPixel];

            // bitmap buffer 
            colorImageBitmap = new WriteableBitmap(
                      colorFrameDescription.Width,
                      colorFrameDescription.Height,
                      96, // dpi-x
                      96, // dpi-y
                      PixelFormats.Bgr32, // pixel format  
                      null);

            // display bitmap on the WPF image
            if (wpfImageForDisplay != null)
                 wpfImageForDisplay.Source = colorImageBitmap;
        }

        private void ColorFrameReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // using statement automatically takes care of disposing of 
            // the ColorFrame object when you are done using it
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                // do nothing when the color frame has no data
                if (colorFrame == null) return;

                // Since we are not using the raw color format as we just want to display the color image
                // convert the data to our desired format first 
                colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Bgra);

                // write the color data on our bitmap           
                colorImageBitmap.WritePixels(
                   new Int32Rect(0, 0,
                   colorFrameDescription.Width, colorFrameDescription.Height), // source rect
                   colorData, // pixel data
                              // stride: width in bytes of a single row of pixel data
                   colorFrameDescription.Width * (int)(colorFrameDescription.BytesPerPixel),
                   0 // offset 
                );
            }
        }
    }
}
