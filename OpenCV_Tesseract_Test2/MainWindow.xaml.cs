using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Tesseract;
using System.Collections.Generic;
using System.Windows.Media;
using System.Text;
using System.Drawing.Imaging;
using System.Threading;

namespace OpenCV_Tesseract_Test2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string DEFAULT_DIR = @"C:\test\screenshots";
        private Bitmap bmp = null;
        private Image<Bgr, Byte> img;
        private Image<Gray, Byte> singleChannelImage;
        private Image<Gray, Byte> processedImage;
        private Image<Gray, Byte> cutImage;

        private string locationName = null;
        private Dictionary<string, System.Windows.Point> locationPoints = new Dictionary<string, System.Windows.Point>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog
            {
                Filter = "Bitmap files|*.bmp",
                Multiselect = false,
                Title = "Select Bitmap for conversion",
                InitialDirectory = DEFAULT_DIR
            };
            if (fd.ShowDialog() == true)
            {
                bmp = new Bitmap(fd.FileName);
                //MessageBox.Show("Dimensions: Height: " + bmp.Height + " width: " + bmp.Width);
                BitmapSource bmpSource = Utils.BitmapToBitmapSource(bmp);
                DisplayCanvas.Height = bmpSource.Height;
                DisplayCanvas.Width = bmpSource.Width;
                DisplayImage.Source = bmpSource;
            }
        }

        private void ConvertImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (bmp != null)
            {
                img = new Image<Bgr, byte>(bmp);

                // Split the image into the 3 channels (R, G, B)
                Image<Gray, Byte>[] splitImg = img.Split();

                // We want the blue channel as this make the text closer to white
                singleChannelImage = splitImg[2];

                // Now we are going to run a binary threshold to try and remove the background
                // The threshold is set high at the moment to remove as much guff as possible
                try
                {
                    int thresholdString = int.Parse(ThresholdValue.Text);
                    Gray grayThreshold = new Gray(thresholdString);


                    processedImage = singleChannelImage.ThresholdToZero(grayThreshold).Not();
                    


                    // Load the bitmap into a BitmapSource for the image controll to display it
                    BitmapSource bmpSource = Utils.BitmapToBitmapSource(processedImage.ToBitmap());

                    // Update the image display to show the processed image
                    DisplayImage.Source = bmpSource;

                }
                catch (Exception exception)
                {
                    if (exception is FormatException || exception is ArgumentNullException)
                    {
                        MessageBox.Show("Error: Exception caught: " + exception.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Error: Image has not been loaded", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point mouseClick = e.GetPosition(DisplayImage);



            Bitmap image = Utils.BitmapImage2Bitmap((BitmapSource)DisplayImage.Source);

            // We want to get a cut image 50 px wide around the mouseClickLocation
            int x = Convert.ToInt16(Math.Floor(mouseClick.X - 25));
            int y = Convert.ToInt16(Math.Floor(mouseClick.Y - 25));


            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(x, y, 50, 50);
            Bitmap newBitmap = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(image, -rect.X, -rect.Y);
            }

            BitmapSource s = Utils.BitmapToBitmapSource(newBitmap);
            PreviewImage.Source = s;

            if (locationName != null)
            {
                drawCircle(sender, mouseClick, locationName);
                if (locationPoints.ContainsKey(locationName))
                {
                    locationPoints.Remove(locationName);
                }
                locationPoints.Add(locationName, mouseClick);
            }
            //MessageBoxResult result = MessageBox.Show("Mouse clicked at x: " + mouseClick.X + " y: " + mouseClick.Y);
        }

        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (processedImage == null)
            {
                MessageBox.Show("The image has not been converted therefore not saving", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                SaveFileDialog fd = new SaveFileDialog()
                {
                    AddExtension = true,
                    Filter = "Bitmap File (*.bmp)|*.bmp",
                    DefaultExt = "*.bmp",
                    InitialDirectory = DEFAULT_DIR,
                    Title = "Save processed image"
                };

                if (fd.ShowDialog() == true)
                {
                    processedImage.ToBitmap().Save(fd.FileName);
                }
            }
        }

        private void IntensityThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MessageBox.Show("Value: " + e.NewValue);
        }

        private void CutImageButton_Click(object sender, RoutedEventArgs e)
        {
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(78, 245, 1096 - 78, 975 - 245);
            DisplayImage.Source = null;
            Bitmap bitmap = processedImage.ToBitmap();
            Bitmap newBitmap = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(bitmap, -rect.X, -rect.Y);
            }
            //bitmap.Clone(rect, bitmap.PixelFormat);
            cutImage = new Image<Gray, byte>(newBitmap);
            BitmapSource bmpSource = Utils.BitmapToBitmapSource(newBitmap);
            DisplayImage.Source = bmpSource;
        }

        private void OCRButton_Click(object sender, RoutedEventArgs e)
        {



            Bitmap commodityData = cutImage.ToBitmap();

            bool processing = true;
            int nonBlankRow = 0;
            int blankRow = 0;
            Window window;
            while (processing)
            {
                nonBlankRow = findNonBlankRow(commodityData, blankRow + 1);
                blankRow = findBlankRow(commodityData, nonBlankRow + 1);

                if (nonBlankRow == -1 || blankRow == -1)
                {
                    processing = false;
                    break;
                }

                int heightOfRow = blankRow - nonBlankRow + 10;

                Bitmap newBitmap = new Bitmap(commodityData.Width, heightOfRow);
                using (Graphics g = Graphics.FromImage(newBitmap))
                {
                    g.DrawImage(commodityData, 0, -(nonBlankRow) + 5);
                }

                BitmapSource bmpSource = Utils.BitmapToBitmapSource(newBitmap);
                DisplayImage.Source = bmpSource;

                //window = new Window();
                //StackPanel panel = new StackPanel();

                //window.Content = panel;
                //window.Width = newBitmap.Width;
                //window.Height = newBitmap.Height;
                //System.Windows.Controls.Image image = new System.Windows.Controls.Image();
                //image.Source = bmpSource;
                //panel.Children.Add(image);
                //window.ShowDialog();

                TesseractEngine tesseract = new TesseractEngine("tessdata", "big", EngineMode.Default);
                var conv = new BitmapToPixConverter();
                int counter = 0;
                var p = conv.Convert(newBitmap);
                Tesseract.Page page = tesseract.Process(p, PageSegMode.SingleLine);

                using (var iter = page.GetIterator())
                {
                    do
                    {
                        MessageBox.Show("Text: " + iter.GetText(PageIteratorLevel.Block) + " Confidence: " + iter.GetConfidence(PageIteratorLevel.Block));
                        counter++;
                        if(counter > 10)
                        {
                            break;
                        }
                    } while (iter.Next(PageIteratorLevel.Block));
                }
                
                page.Dispose();
                newBitmap.Dispose();
                tesseract.Dispose();



                Thread.Sleep(1000);




            }
            //int nonBlankRow = findNonBlankRow(commodityData, 0);
            //int blankRow = findBlankRow(commodityData, nonBlankRow+1);

            //MessageBox.Show("Non-white: " + nonBlankRow + " White: " + blankRow);

            //nonBlankRow = findNonBlankRow(commodityData, blankRow);
            //blankRow = findBlankRow(commodityData, nonBlankRow + 1);

            //MessageBox.Show("Non-white: " + nonBlankRow + " White: " + blankRow);


            //using (tesseract)
            //{
            //    Tesseract.Page ocrResult = tesseract.Process(cutImage.ToBitmap());
            //    StringBuilder builder = new StringBuilder();

            //    using (var iter = ocrResult.GetIterator())
            //    {
            //        int i = 1;
            //        do
            //        {
            //            var text = iter.GetText(PageIteratorLevel.TextLine);
            //            builder.Append("Line ").Append(i).Append(": ").Append(text).Append(" Confidence: ")
            //                .Append(iter.GetConfidence(PageIteratorLevel.TextLine)).AppendLine();
            //            i++;
            //        } while (iter.Next(PageIteratorLevel.TextLine));
            //    }
            //    MessageBox.Show("Text: '" + builder.ToString());
            //}
        }

        private int findNonBlankRow(Bitmap bitmap, int startingRow)
        {
            for (int y = startingRow; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    System.Drawing.Color pixelColor = bitmap.GetPixel(x, y);
                    if (pixelColor.R < 250 || pixelColor.G < 250 || pixelColor.B < 250)
                    {
                        return y;
                    }
                }
            }

            return -1;
        }

        private int findBlankRow(Bitmap bitmap, int startingRow)
        {
            for (int y = startingRow; y < bitmap.Height; y++)
            {
                bool nonBlankFound = false;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    System.Drawing.Color pixelColor = bitmap.GetPixel(x, y);
                    if (pixelColor.R < 250 || pixelColor.G < 250 || pixelColor.B < 250)
                    {
                        nonBlankFound = true;
                        break;
                    }
                }

                if (nonBlankFound == false)
                {
                    return y;
                }
            }

            return -1;
        }

        private void Location_Click(object sender, RoutedEventArgs e)
        {
            RadioButton source = (RadioButton)sender;

            locationName = source.Name;
        }

        private void drawCircle(object sender, System.Windows.Point location, string locationName)
        {
            Canvas canvas = sender as Canvas;
            System.Windows.Media.Color strokeColour = System.Windows.Media.Colors.Cyan;

            if (canvas != null)
            {

                TextBlock textBlock = new TextBlock();
                textBlock.Name = locationName + "_text";
                string locationNumber = locationName.Substring(13);
                textBlock.Text = locationNumber;
                textBlock.Foreground = new SolidColorBrush(strokeColour);
                Canvas.SetLeft(textBlock, location.X + 12);
                Canvas.SetTop(textBlock, location.Y + 12);

                System.Windows.Shapes.Ellipse e = new System.Windows.Shapes.Ellipse();
                e.Name = locationName + "_circle";
                e.Width = 30;
                e.Height = 30;
                e.Stroke = new System.Windows.Media.SolidColorBrush(strokeColour);
                Canvas.SetTop(e, location.Y - 15);
                Canvas.SetLeft(e, location.X - 15);

                System.Windows.Shapes.Line hl = new System.Windows.Shapes.Line();
                hl.Name = locationName + "_hline";
                hl.X1 = location.X - 15;
                hl.X2 = location.X + 15;
                hl.Y1 = location.Y;
                hl.Y2 = location.Y;
                hl.Stroke = new System.Windows.Media.SolidColorBrush(strokeColour);
                Canvas.SetTop(hl, 0);
                Canvas.SetTop(hl, 0);

                System.Windows.Shapes.Line vl = new System.Windows.Shapes.Line();
                vl.Name = locationName + "_vline";
                vl.X1 = location.X;
                vl.X2 = location.X;
                vl.Y1 = location.Y - 15;
                vl.Y2 = location.Y + 15;
                vl.Stroke = new System.Windows.Media.SolidColorBrush(strokeColour);
                Canvas.SetTop(vl, 0);
                Canvas.SetTop(vl, 0);

                // Need to think of a better way to do this, using lamdas perhaps
                foreach (string elementName in new string[] { locationName + "_circle", locationName + "_hline", locationName + "_vline", locationName + "_text" })
                {
                    foreach (UIElement element in canvas.Children)
                    {
                        FrameworkElement fe = element as FrameworkElement;
                        if (e != null)
                        {
                            if (fe.Name.Equals(elementName))
                            {
                                canvas.Children.Remove(element);
                                break;
                            }
                        }
                    }
                }
                canvas.Children.Add(e);
                canvas.Children.Add(hl);
                canvas.Children.Add(vl);
                canvas.Children.Add(textBlock);
            }
        }

        private void LocationsButton_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string location in locationPoints.Keys)
            {
                System.Windows.Point p;
                locationPoints.TryGetValue(location, out p);
                builder.Append(location).Append("- X:").Append(p.X).Append(" Y:").Append(p.Y).AppendLine();
            }

            MessageBox.Show(builder.ToString());
        }
    }
}
