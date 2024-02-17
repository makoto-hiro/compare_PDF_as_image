using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Windows.Data.Pdf;
using System.Diagnostics;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace compare_PDF_as_image
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private Windows.Data.Pdf.PdfDocument pdfDocument;
        //private List<BitmapSource> pdfPages = new List<BitmapSource>();
        private int displayedPageNumber = 1;
        private System.Windows.Point startPoint;
        private System.Windows.Point startPosition;
        private List<Mat> pdfPages1 = new List<Mat>();
        private List<Mat> pdfPages2 = new List<Mat>();
        private string filePath1 = "";
        private string filePath2 = "";

        private async Task ReadPDFtoImage(string filename, string docID)
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(System.IO.Path.GetFullPath(filename));

            try
            {
                pdfDocument = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
            }
            catch
            {
            }

            if (pdfDocument != null)
            {
                for (uint i = 0; i < pdfDocument.PageCount; i++)
                {
                    using (Windows.Data.Pdf.PdfPage page = pdfDocument.GetPage(i))
                    {
                        using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                        {
                            PdfPageRenderOptions renderOptions = new PdfPageRenderOptions();
                            renderOptions.DestinationWidth = (uint)Math.Round(page.Dimensions.ArtBox.Width / 96.0 * 300.0);
                            await page.RenderToStreamAsync(stream, renderOptions);
                            PngBitmapDecoder decoder = new PngBitmapDecoder(stream.AsStream(), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                            BitmapSource renderedPage = (BitmapSource)decoder.Frames[0];
                            Mat colorMat = BitmapSourceConverter.ToMat(renderedPage);
                            Mat grayMat = new Mat();
                            Cv2.CvtColor(colorMat, grayMat, ColorConversionCodes.BGR2GRAY);
                            Mat blackwhiteMat = new Mat();
                            Cv2.Threshold(grayMat, blackwhiteMat, 250, 255, ThresholdTypes.Binary);
                            if (docID == "2")
                            {
                                pdfPages2.Add(blackwhiteMat);
                            }
                            else
                            {
                                pdfPages1.Add(blackwhiteMat);
                            }
                        }
                    }
                }
            }
        }
        private void ShowPage(int pageNumber1, int pageNumber2)
        {
            // ページ数が表示の条件に合わない場合は、何もしない。
            if (pageNumber1 < 1) return;
            if (pdfPages1.Count == 0) return;
            if (pageNumber1 > pdfPages1.Count) return;
            if (pdfPages2.Count < pageNumber2) return;

            // ページのピクセルサイズが異なる場合は、page2をリサイズする。
            Mat modifiedMat1 = new Mat();
            Mat modifiedMat2 = new Mat();
            OpenCvSharp.Size pageSize1 = pdfPages1[pageNumber1 - 1].Size();
            OpenCvSharp.Size pageSize2 = pdfPages2[pageNumber2 - 1].Size();
            if ((pageSize1.Height != pageSize2.Height) || (pageSize1.Width != pageSize2.Width))
            {
                int biggerHeight = Math.Max(pageSize1.Height, pageSize2.Height);
                int biggerWidth = Math.Max(pageSize1.Width, pageSize2.Width);
                OpenCvSharp.Size adjustedSize = new OpenCvSharp.Size();
                adjustedSize.Height = biggerHeight;
                adjustedSize.Width = biggerWidth;
                Cv2.Resize(pdfPages1[pageNumber1 - 1], modifiedMat1, adjustedSize);
                Cv2.Resize(pdfPages2[pageNumber2 - 1], modifiedMat2, adjustedSize);
            }
            else
            {
                modifiedMat1 = pdfPages1[pageNumber1 - 1];
                modifiedMat2 = pdfPages2[pageNumber2 - 1];
            }

            // ２つのページの共通部分の画像を作る。
            Mat msk = new Mat();
            Cv2.BitwiseAnd(modifiedMat1, modifiedMat2, msk);

            // 比較を表示するための画像を作る。
            Mat m = new Mat();
            Cv2.Merge(new Mat[] { modifiedMat2, msk , modifiedMat1 }, m);

            // 比較画像を表示する。
            BitmapSource img = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m);
            imgMain.Source = img;

            // ページ番号を表示する。
            txtPage.Text = "Page " + displayedPageNumber.ToString() + " / " + pdfPages1.Count.ToString();

            // 各々の画像のピクセルサイズを表示する。
            OpenCvSharp.Size _s1 = pdfPages1[pageNumber1 - 1].Size();
            txtFile1SizeInfo.Text = "H : " + _s1.Height.ToString() + " / W : " + _s1.Width.ToString();
            OpenCvSharp.Size _s2 = pdfPages2[pageNumber2 - 1].Size();
            txtFile2SizeInfo.Text = "H : " + _s2.Height.ToString() + " / W : " + _s2.Width.ToString();

            // 比較画像の情報を表示する。
            string tp = "Page : " + pageNumber1.ToString() + " / " + pdfPages1.Count.ToString();
            string ph = "PixelHeight : " + img.PixelHeight.ToString();
            string pw = "PixelWidth : " + img.PixelWidth.ToString();
            txtInfo.Text = tp + "\n" + ph + "\n" + pw;
        }

        private void menuNext_Click(object sender, RoutedEventArgs e)
        {
            if (displayedPageNumber < pdfPages1.Count)
            {
                displayedPageNumber++;
                ShowPage(displayedPageNumber, displayedPageNumber);
            }
            else
            {
                return;
            }
        }

        private void menuPrev_Click(object sender, RoutedEventArgs e)
        {
            if (displayedPageNumber > 1)
            {
                displayedPageNumber--;
                ShowPage(displayedPageNumber, displayedPageNumber);
            }
            else
            {
                return;
            }
        }

        private void MenuExport_Click(object sender, RoutedEventArgs e)
        {
            if (pdfPages1.Count < 1) return;

            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.DefaultExt = ".png";
            dialog.Filter = "PNG files (.png)|*.png";
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string filename = dialog.FileName;
                using (var fs = new FileStream(filename, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    BitmapSource img = WriteableBitmapConverter.ToWriteableBitmap(pdfPages1[displayedPageNumber - 1]);
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(fs);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Process currentProcess = Process.GetCurrentProcess();
            Process pc = new Process();
            pc.StartInfo.FileName = "close_pdf_trial.bat";
            pc.StartInfo.Arguments = currentProcess.Id.ToString();
            pc.StartInfo.CreateNoWindow = true;
            pc.StartInfo.UseShellExecute = false;
            pc.Start();
        }

        private void ScvMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (scvMain.IsMouseCaptureWithin) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ScrollViewer sc = (ScrollViewer)sender;
                System.Windows.Point pt = e.GetPosition((ScrollViewer)sender);
                sc.ScrollToVerticalOffset(startPosition.Y + (pt.Y - startPoint.Y) * -1);
                sc.ScrollToHorizontalOffset(startPosition.X + (pt.X - startPoint.X) * -1);

                txtPointerInfo.Text = "Mouse LeftButtonDown\n X :" + startPoint.X.ToString() + "\n Y :" + startPoint.Y.ToString();
                txtPointerInfo.Text = txtPointerInfo.Text + "\nMouseMove\n X : " + pt.X.ToString() + "\n Y : " + pt.Y.ToString();
            }
        }

        private void ScvMain_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition((ScrollViewer)sender);
            ScrollViewer sc = (ScrollViewer)sender;
            startPosition = new System.Windows.Point(sc.HorizontalOffset, sc.VerticalOffset);

            txtPointerInfo.Text = "Mouse LeftButtonDown\n X :" + startPoint.X.ToString() + "\n Y :" + startPoint.Y.ToString();
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e != null)
            {
                double _ns = e.NewValue / 100;
                Matrix mx = new Matrix();
                mx.Scale(_ns, _ns);
                imgMain.LayoutTransform = new MatrixTransform(mx);
            }
        }

        private async void BtnOpenFile1_Click(object sender, RoutedEventArgs e)
        {
            string btnID = ((Button)sender).Tag.ToString();

            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.DefaultExt = ".pdf";
            dialog.Filter = "PDF files (.pdf)|*.pdf";
            bool? result = dialog.ShowDialog();

            if (result != true) return;
            string filename = dialog.FileName;
            if (btnID == "2")
            {
                txtFile2Info.Text = System.IO.Path.GetFileName(filename);
                filePath2 = filename;
            }
            else
            {
                txtFile1Info.Text = System.IO.Path.GetFileName(filename);
                filePath1 = filename;
            }
            if ((txtFile1Info.Text != "") && (txtFile2Info.Text !=""))
            {
                if ((pdfPages2.Count < 1) && (pdfPages2.Count < 1))
                {
                    pdfPages1.Clear();
                    txtFile1Info.Foreground = new SolidColorBrush(Colors.Red);
                    txtFile1Info.Text = "ファイル読み込み中";
                    await ReadPDFtoImage(filePath1, "1");
                    txtFile1Info.Foreground = new SolidColorBrush(Colors.Black);
                    txtFile1Info.Text = System.IO.Path.GetFileName(filePath1);
                    pdfPages2.Clear();
                    txtFile2Info.Foreground = new SolidColorBrush(Colors.Red);
                    txtFile2Info.Text = "ファイル読み込み中";
                    await ReadPDFtoImage(filePath2, "2");
                    txtFile2Info.Foreground = new SolidColorBrush(Colors.Black);
                    txtFile2Info.Text = System.IO.Path.GetFileName(filePath2);
                }
                else
                {
                    if (btnID == "2")
                    {
                        pdfPages2.Clear();
                        txtFile2Info.Foreground = new SolidColorBrush(Colors.Red);
                        txtFile2Info.Text = "ファイル読み込み中";
                        await ReadPDFtoImage(filePath2, "2");
                        txtFile2Info.Foreground = new SolidColorBrush(Colors.Black);
                        txtFile2Info.Text = System.IO.Path.GetFileName(filePath2);
                    }
                    else
                    {
                        pdfPages1.Clear();
                        txtFile1Info.Foreground = new SolidColorBrush(Colors.Red);
                        txtFile1Info.Text = "ファイル読み込み中";
                        await ReadPDFtoImage(filePath1, "1");
                        txtFile1Info.Foreground = new SolidColorBrush(Colors.Black);
                        txtFile1Info.Text = System.IO.Path.GetFileName(filePath1);
                    }
                }
            }
            /*
            if (result == true)
            {
                if (btnID == "2")
                {
                    pdfPages2.Clear();
                    txtFile2Info.Foreground = new SolidColorBrush(Colors.Red);
                    txtFile2Info.Text = "ファイル読み込み中";
                    string filename = dialog.FileName;
                    await ReadPDFtoImage(filename, btnID);
                    txtFile2Info.Foreground = new SolidColorBrush(Colors.Black);
                    txtFile2Info.Text = System.IO.Path.GetFileName(filename);
                }
                else
                {
                    pdfPages1.Clear();
                    txtFile1Info.Foreground = new SolidColorBrush(Colors.Red);
                    txtFile1Info.Text = "ファイル読み込み中";
                    string filename = dialog.FileName;
                    await ReadPDFtoImage(filename, btnID);
                    txtFile1Info.Foreground = new SolidColorBrush(Colors.Black);
                    txtFile1Info.Text = System.IO.Path.GetFileName(filename);

                }

            }
            */
            displayedPageNumber = 1;
            ShowPage(displayedPageNumber, displayedPageNumber);

        }

        private void MenuLicense_Click(object sender, RoutedEventArgs e)
        {
            string _msg = "このソフトウェアは、 Apache 2.0ライセンスで配布されている製作物が含まれています。";
            _msg = _msg + "\nhttp://www.apache.org/licenses/LICENSE-2.0";
            _msg = _msg + "\nこのソフトウェアは、OpenCVSharpおよびそれが依存するソフトウェアを利用しています。";
            MessageBox.Show(_msg);
        }
    }
}
