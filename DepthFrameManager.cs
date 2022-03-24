// Kinect V2 API
using Microsoft.Kinect;
// .NET-based APIs from Microsoft
using System.Windows.Media.Imaging;
using System.Drawing;
using System;
using System.Media;
using System.Windows.Media;
using System.Windows;
// Emgu CV is a cross platform .Net wrapper to the OpenCV image processing library.
// Allowing OpenCV functions to be called from .NET compatible languages.
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

// using the whole frame work of T5_BodilyInteraction
namespace NUI3D
{
    class DepthFrameManager
    {
        private KinectSensor sensor;
        // to receive and manipulate the depth image
        private FrameDescription depthFrameDescription = null;
        private ushort[] depthData = null;
        private int bytesPerPixel = 4;
        private byte[] depthPixels = null;
        // to display the depth image
        private WriteableBitmap depthImageBitmap = null;
        private System.Windows.Controls.Image wpfImage = null;

        // to receive the player's body index data
        private byte[] bodyIndexData = null;
        
        // the circle that the player control with hands
        // the circle will adjust its size according to the average depth
        private CircleF cir = new CircleF(new System.Drawing.PointF(100f, 100f), 10);
        private double avg_depth = 0;

        // my own contribution
        // to map the depth and position data with the color channel
        // so that the circle will keep changing its color
        // whenever the depth or position data changes
        private float depthToColor = 0;
        private float xToColor = 0;
        private float yToColor = 0;

        // the rectangle that will spawn in a random position
        // and move in a certain direction to hit the circle
        private Rectangle rect = new Rectangle(200, 0, 50, 50);
        Random random = new Random();
        private bool moveX = false;
        private bool moveY = true;
        private int acceleration = 5;
        // when the rectangle hit the boundary of the screen 
        // it will play a sound
        SoundPlayer player = new SoundPlayer("gunshot.wav");

        // the score for the game
        // the longer the player survive the game 
        // and the bigger the size of the circle
        // the score will be bigger
        private static int score = 0;
        public static int Score
        {
            get { return score; }
            set { score = value; }
        }
        System.Windows.Controls.TextBox scoreTextBox;
        
        // the end of my contribution
        public void Init(KinectSensor s, System.Windows.Controls.Image wpfImageForDisplay, System.Windows.Controls.TextBox textBox)
        {
            sensor = s;
            wpfImage = wpfImageForDisplay;
            scoreTextBox = textBox;
            DepthFrameReaderInit();
            BodyIndexFrameReaderInit();
        }

        private void DepthFrameReaderInit()
        {
            // Open the reader for the depth frames
            DepthFrameReader depthFrameReader = sensor.DepthFrameSource.OpenReader();

            // register an event handler for FrameArrived 
            depthFrameReader.FrameArrived += DepthFrameReader_FrameArrived;

            // allocate storage for depth data
            depthFrameDescription = sensor.DepthFrameSource.FrameDescription;
            // 16 - bit unsigned integer per pixel
            depthData = new ushort[depthFrameDescription.LengthInPixels];

            // initialization for displaying depth data
            // to associate 4-byte color for each pixel             
            depthPixels = new byte[depthFrameDescription.LengthInPixels * bytesPerPixel];
            
            // to get the depth frame properties from the kinect depth frame description
            depthImageBitmap = new WriteableBitmap(
                                       depthFrameDescription.Width, // 512 
                                       depthFrameDescription.Height, // 424
                                       96, 96, PixelFormats.Bgr32, null);

            // display depth image on the WPF image
            if(wpfImage!=null)
                wpfImage.Source = depthImageBitmap;

        }

        private void DepthFrameReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame == null) return;
                depthFrame.CopyFrameDataToArray(depthData);

                // to get the drawing of the approximate hand depth image
                System.Drawing.Bitmap bmp = HandSegmentation();
                
                if (bmp != null)
                    ContourDetectionAndVisualization(bmp);
            }
        }

        // -----------------------------------------------------------------------------
        private void BodyIndexFrameReaderInit() // call it in Window_Loaded 
        {
            // Open the reader for body index frames 
            BodyIndexFrameReader bodyIndexFrameReader = sensor.BodyIndexFrameSource.OpenReader();

            bodyIndexFrameReader.FrameArrived += BodyIndexFrameReader_FrameArrived;

            // Body index frame has the same resolution as the depth frame 
            // Each pixel is represented by an 8-bit unsigned integer 
            bodyIndexData = new byte[sensor.DepthFrameSource.FrameDescription.LengthInPixels];
        }

        private void BodyIndexFrameReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
        {
            using (BodyIndexFrame bodyIndexFrame =
                    e.FrameReference.AcquireFrame())
            {
                if (bodyIndexFrame == null) return;
                // copy the body index data to the bodyIndexData array
                bodyIndexFrame.CopyFrameDataToArray(bodyIndexData);
            }
        }

        private void ContourDetectionAndVisualization(System.Drawing.Bitmap bmp)
        {
            if (bmp == null) return;

            // code adapted from T4_BlobDetection 
            // Bitmap -> Image (using the extension method from Emgu.CV.BitmapExtension) 
            Image<Bgr, byte> openCVImg = bmp.ToImage<Bgr, byte>();
                
            // convert from Bgr to gray 
            Image<Gray, byte> grayImg = openCVImg.Convert<Gray, byte>();

            // contour detection and visualization 
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // outer contours only
                CvInvoke.FindContours(grayImg, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                for (int i = 0; i < contours.Size; i++)
                {
                    double area = CvInvoke.ContourArea(contours[i]);
                    // Filter out contours of small size
                    if (area < 20 * 20) continue;

                    openCVImg.Draw(contours, i, new Bgr(System.Drawing.Color.Red), 2);

                    RotatedRect rotatedRect = CvInvoke.MinAreaRect(contours[i]);

                    avg_depth = CalculateAverageDepth(grayImg, contours[i]) / 1000;

                    // The following parts is my own contribution

                    updateRect();

                    // control the circle position with the hand depth image

                    float xMovement = 0;
                    float yMovement = 0;

                    // when the hand depth image's center is in the right part of the screen
                    // move the circle towards right direction
                    if (rotatedRect.Center.X > (depthFrameDescription.Width / 2))
                        xMovement = (rotatedRect.Center.X / 100) * 2;

                    // when the hand depth image's center is in the left part of the screen
                    // move the circle towards left direction
                    if (rotatedRect.Center.X < (depthFrameDescription.Width / 2))
                        xMovement = -(rotatedRect.Center.X / 100) * 2;

                    // when the hand depth image's center is in the bottom part of the screen
                    // move the circle towards bottom direction
                    if (rotatedRect.Center.Y > (depthFrameDescription.Height / 2))
                        yMovement = (rotatedRect.Center.Y / 100) * 2;

                    // when the hand depth image's center is in the top part of the screen
                    // move the circle towards top direction
                    if (rotatedRect.Center.Y < (depthFrameDescription.Height / 2))
                        yMovement = - (rotatedRect.Center.Y / 100) * 2;

                    updateCircle(xMovement, yMovement);

                    // calculate the score
                    Score += (int)cir.Radius;
                    scoreTextBox.Text = Score.ToString();

                    // when the circle hit by the rectangle
                    collisionDetection();

                    // map the depth data, and position data to the color channel of circle
                    depthToColor = (float)avg_depth * 255;
                    xToColor = (rotatedRect.Center.X / depthFrameDescription.Width) * 255;
                    yToColor = (rotatedRect.Center.Y / depthFrameDescription.Height) * 255;
                }
            }

            // Draw the rectangle and the circle
            openCVImg.Draw(rect, new Bgr(0, 0, 255), -1);
            openCVImg.Draw(cir, new Bgr(xToColor, depthToColor, yToColor), 0 /* fill up */);
            // the end of my contribution

            // display the processed image 
            bmp = openCVImg.ToBitmap<Bgr, byte>(); // extension method
            wpfImage.Source = ImageHelpers.ToBitmapSource(bmp);
        }
        // The following parts is my own contribution
        private void updateRect()
        {
            int newX, newY;

            // update the rectangle according to the mode of X and Y
            if (moveY) rect = new Rectangle(rect.X, rect.Y + acceleration, 50, 50);
            if (moveX) rect = new Rectangle(rect.X + acceleration, rect.Y, 50, 50);

            // reset the position of the rectangle if it move outside the range the screen
            if (rect.X > depthFrameDescription.Width || rect.Y > depthFrameDescription.Height)
            {
                player.Play();
                moveX = random.NextDouble() >= 0.5;

                if (moveX)
                {
                    // X movement
                    moveY = false;
                    newX = 0;
                    newY = random.Next(depthFrameDescription.Height);
                }
                else
                {
                    // Y movement
                    moveY = true;
                    newX = random.Next(depthFrameDescription.Width);
                    newY = 0;
                }

                ++acceleration;
                rect = new Rectangle(newX, newY, 50, 50);
            }
        }
        // smoothly transit the value between a and b
        private float lerp(float a, float b, float w)
        {
            float result = a * (1 - w) + b * w;
            // to prevent a circle with a radius of negative value to be created
            while (result < 0) result = a * (1 - w) + b * w;
            return result;
        }

        private void updateCircle(float sX, float sY)
        {
            float newY = cir.Center.Y + sY;
            float newX = cir.Center.X + sX;
            float newSize = lerp(70, 20, (float)avg_depth * 2);

            // limit the circle and don't let it get out of the screen range
            if (newY > depthFrameDescription.Height) newY = depthFrameDescription.Height;
            if (newY < 0) newY = 0;
            if (newX > depthFrameDescription.Width) newX = depthFrameDescription.Width;
            if (newX < 0) newX = 0;

            // update the circle
            cir = new CircleF(new System.Drawing.PointF(newX, newY), newSize);
        }

        private void collisionDetection()
        {
            // detect when the circle hit by the rectangle
            bool hit = false;

            // detect center only so that it is easier for the player to play
            //if (cir.Center.X > rect.X && cir.Center.X < (rect.X + rect.Width)
            //    && cir.Center.Y > rect.Y && cir.Center.Y < (rect.Y + rect.Height))
            //{
            //   hit = true;
            //}

            // detect four points collision between the circle and the rectangle
            float leftX = cir.Center.X - cir.Radius;
            if (leftX > rect.X && leftX < (rect.X + rect.Width)
                && cir.Center.Y > rect.Y && cir.Center.Y < (rect.Y + rect.Height))
            {
                hit = true;
            }
            float rightX = cir.Center.X + cir.Radius;
            if (rightX > rect.X && rightX < (rect.X + rect.Width)
                && cir.Center.Y > rect.Y && cir.Center.Y < (rect.Y + rect.Height))
            {
                hit = true;
            }
            float topY = cir.Center.Y - cir.Radius;
            if (cir.Center.X > rect.X && cir.Center.X < (rect.X + rect.Width)
                && topY > rect.Y && topY < (rect.Y + rect.Height))
            {
                hit = true;
            }
            float downY = cir.Center.Y + cir.Radius;
            if (cir.Center.X > rect.X && cir.Center.X < (rect.X + rect.Width)
                && downY > rect.Y && downY < (rect.Y + rect.Height))
            {
                hit = true;
            }
            // Show game result when the circle is hit by the rectangle
            if (hit)
            {
                // show the result
                MessageBox.Show("GAME OVER! \n" +
                    "Your Score Is : " + Score.ToString());
                // close the application
                System.Environment.Exit(0);
            }
        }

        // the end of my contribution

        // Calculate the average depth for all the depth pixels within a given contour. 
        // You should make sure depthData contains the most updated depth data.
        private double CalculateAverageDepth(Image<Gray, byte> binaryImg, IInputArray contour)
        {
            double avg_depth = 0;
            int count = 0;
            System.Drawing.Rectangle aabb = CvInvoke.BoundingRectangle(contour);
            for (int col = aabb.Left; col < aabb.Right; col++)
                for (int row = aabb.Top; row < aabb.Bottom; row++)
                {
                    byte pixel = binaryImg.Data[row, col, 0]; // get corresponding pixel 
                    if (pixel == 255) // white
                    {
                        avg_depth += depthData[row * depthFrameDescription.Width + col];
                        count++;
                    }
                }
            if (count != 0) return avg_depth / count;
            else return 0;
        }

        // -----------------------------------------------------------------
        private System.Drawing.Bitmap HandSegmentation()
        {
            // estimate the depth of human body                         
            float d_body = 0;
            int num_of_player_pixels = 0; // for Test 2; class exercise 3           

            for (int i = 0; i < depthData.Length; ++i)
            {                
                if (bodyIndexData[i] != 255) // player                
                {
                    // Test 2:
                    // d_body = average depth of player segmentation
                    d_body += depthData[i];
                    num_of_player_pixels++;
                }
            }

            // Test 2: class exercise 3           
            // d_body = average depth of player segmentation data
            if (num_of_player_pixels != 0) d_body /= num_of_player_pixels;

            // the offset from the average body depth
            // so that it is easier to distinguish the hands from the body
            float offset = 150; // mm 

            for (int i = 0; i < depthData.Length; ++i)
            {
                ushort depth = depthData[i];
                float handSegmentationDepth = d_body - offset;
                if (bodyIndexData[i] != 255 && depth < handSegmentationDepth)
                { 
                    // draw hand with white pixels                   
                    depthPixels[4 * i] = 255;
                    depthPixels[4 * i + 1] = 255;
                    depthPixels[4 * i + 2] = 255;
                }
                else // non-hand (including the background) 
                {   
                    // draw other non-hand things with black color
                    depthPixels[4 * i] = 0; // blue byte
                    depthPixels[4 * i + 1] = 0;
                    depthPixels[4 * i + 2] = 0;
                }                
            }

            // write all the depth pixels on the bitmap source
            BitmapSource bmpSrc = BitmapSource.Create(depthFrameDescription.Width, depthFrameDescription.Height, 96, 96,
                                        PixelFormats.Bgr32, null,
                                depthPixels, depthFrameDescription.Width * 4);
            // Convert BitmapSource -> Bitmap 
            return ImageHelpers.ToBitmap(bmpSrc);
        }
    }
}
