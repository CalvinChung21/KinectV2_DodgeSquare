// Kinect V2 API
using Microsoft.Kinect;
// .NET-based APIs from Microsoft
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// using the whole frame work of T5_BodilyInteraction
namespace NUI3D
{
    public class ImageHelpers
    {
        // detect objects in the depth range [min, max]
        // detected objects will be displayed in white color
        public static BitmapSource SliceDepthImage(DepthFrame image, int min = 1000, int max = 1200)
        {
            FrameDescription frameDescription = image.FrameDescription;
            ushort[] rawDepthData = new ushort[frameDescription.LengthInPixels];
            image.CopyFrameDataToArray(rawDepthData);

            byte[] pixels = new byte[frameDescription.LengthInPixels * 4];

            for (int i = 0; i < rawDepthData.Length; ++i)
            {
                ushort depth = rawDepthData[i];

                if (depth > min && depth < max)
                {
                    // Set the pixels in the range to white 
                    pixels[4 * i] = 255; //blue
                    pixels[4 * i + 1] = 255; //green
                    pixels[4 * i + 2] = 255; //red                    
                }
            }

            return BitmapSource.Create(frameDescription.Width, frameDescription.Height, 96, 96, PixelFormats.Bgr32, null, pixels, frameDescription.Width * 4);
        }

        // BitmapSource --> Bitmap 
        // Reference code: https://stackoverflow.com/questions/3751715/convert-system-windows-media-imaging-bitmapsource-to-system-drawing-image/30380876
        public static System.Drawing.Bitmap ToBitmap(BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (var outStream = new MemoryStream())
            {
                // from System.Media.BitmapImage to System.Drawing.Bitmap
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
                return bitmap;
            }
        }

        // Bitmap --> BitmapSource 
        // Reference code: https://stackoverflow.com/questions/30727343/fast-converting-bitmap-to-bitmapsource-wpf
        public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }
    }
}
