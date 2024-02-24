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
using System.Printing;
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
        private int displayedPageNumber = 1;
        private System.Windows.Point startPoint;
        private System.Windows.Point startPosition;
        private List<Mat> pdfPages1 = new List<Mat>();
        private List<Mat> pdfPages2 = new List<Mat>();
        private string filePath1 = "";
        private string filePath2 = "";
        private System.Windows.Point canvasStartPoint;
        private System.Windows.Point canvasStartPosition;
        private Mat display1;
        private Mat display2;
        private Mat thickness1;
        private Mat thickness2;

        private List<Mat> AdjustMatSize(Mat m1, Mat m2)
        {
            var adjustedM1 = new Mat();
            var adjustedM2 = new Mat();
            var returnMats = new List<Mat>();

            OpenCvSharp.Size s1 = m1.Size();
            OpenCvSharp.Size s2 = m2.Size();
            if ((s1.Height != s2.Height) || (s1.Width != s2.Width))
            {
                int biggerHeight = Math.Max(s1.Height, s2.Height);
                int biggerWidth = Math.Max(s1.Width, s2.Width);
                OpenCvSharp.Size adjustedSize = new OpenCvSharp.Size();
                adjustedSize.Height = biggerHeight;
                adjustedSize.Width = biggerWidth;
                Cv2.Resize(m1, adjustedM1, adjustedSize);
                Cv2.Resize(m2, adjustedM2, adjustedSize);
            }
            else
            {
                adjustedM1 = m1.Clone();
                adjustedM2 = m2.Clone();
            }

            returnMats.Add(adjustedM1);
            returnMats.Add(adjustedM2);

            return returnMats;
        }

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
                            // レンダリングする際の解像度を決める。規定値は300DPI。A2で300DPI相当のデータ量になるように解像度を調整する。
                            double pageMaxLength = Math.Max(page.Dimensions.ArtBox.Height, page.Dimensions.ArtBox.Width);
                            double renderResolution = 300.0;
                            if (pageMaxLength > 1594)
                            {
                                renderResolution = 300 / (pageMaxLength / 1594);
                            }
                            renderOptions.DestinationWidth = (uint)Math.Round(page.Dimensions.ArtBox.Width / 96.0 * renderResolution);
                            // ページをレンダリングする。
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
            cvsMain.Width = img.PixelWidth;
            cvsMain.Height = img.PixelHeight;

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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // アプリでの終了を無効にする。
            e.Cancel = true;

            // アプリのプロセスIDを取得する。
            Process currentProcess = Process.GetCurrentProcess();

            // 外部のkillプログラムを実行する。
            var killProcess = new Process();
            killProcess.StartInfo.FileName = "close_compare_PDF_as_image.bat";
            killProcess.StartInfo.Arguments = currentProcess.Id.ToString();
            killProcess.StartInfo.CreateNoWindow = true;
            killProcess.StartInfo.UseShellExecute = false;
            killProcess.Start();
        }

        private void MenuLicense_Click(object sender, RoutedEventArgs e)
        {
            string msg = "compare_PDF_as_image ver.0.0.0.2";
            msg = msg + "\n\nこのソフトウェアは、 Apache 2.0ライセンスで配布されている製作物が含まれています。";
            msg = msg + "\nhttp://www.apache.org/licenses/LICENSE-2.0";
            msg = msg + "\nこのソフトウェアは、OpenCVSharpおよびそれが依存するソフトウェアを利用しています。";
            msg = msg + "\n\nこのソフトウェアにはMITライセンスが適用されます。";
            msg = msg + "\nhttps://licenses.opensource.jp/MIT/MIT.html";
            msg = msg + "\n(c) 2024 makoto-hiro@GitHub";

            MessageBox.Show(msg);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (pdfPages1.Count < 1) return;
            if (pdfPages2.Count < 1) return;

            var diag = new PrintDialog();
            var result = diag.ShowDialog();

            if (result != true) return;

            var printDoc = new FixedDocument();

            int pageNum = Math.Min((int)pdfPages1.Count, (int)pdfPages2.Count);
            for (int i = 0; i < pageNum; i++)
            {
                Mat modifiedMat1 = new Mat();
                Mat modifiedMat2 = new Mat();
                OpenCvSharp.Size pageSize1 = pdfPages1[i].Size();
                OpenCvSharp.Size pageSize2 = pdfPages2[i].Size();
                if ((pageSize1.Height != pageSize2.Height) || (pageSize1.Width != pageSize2.Width))
                {
                    int biggerHeight = Math.Max(pageSize1.Height, pageSize2.Height);
                    int biggerWidth = Math.Max(pageSize1.Width, pageSize2.Width);
                    OpenCvSharp.Size adjustedSize = new OpenCvSharp.Size();
                    adjustedSize.Height = biggerHeight;
                    adjustedSize.Width = biggerWidth;
                    Cv2.Resize(pdfPages1[i], modifiedMat1, adjustedSize);
                    Cv2.Resize(pdfPages2[i], modifiedMat2, adjustedSize);
                }
                else
                {
                    modifiedMat1 = pdfPages1[i];
                    modifiedMat2 = pdfPages2[i];
                }

                // ２つのページの共通部分の画像を作る。
                Mat msk = new Mat();
                Cv2.BitwiseAnd(modifiedMat1, modifiedMat2, msk);

                // 比較を表示するための画像を作る。
                Mat m = new Mat();
                Cv2.Merge(new Mat[] { modifiedMat2, msk, modifiedMat1 }, m);
                //Cv2.Resize(m, m, new OpenCvSharp.Size(diag.PrintableAreaWidth, diag.PrintableAreaHeight));

                // 比較画像を表示する。
                BitmapSource img = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m);
                var printImage = new Image();
                printImage.Source = img;


                var printCanvas = new Canvas();
                //printCanvas.Width = diag.PrintableAreaWidth;
                //printCanvas.Height = diag.PrintableAreaHeight;
                printCanvas.Children.Add(printImage);

                var printPage = new FixedPage();
                printPage.Children.Add(printCanvas);
                var printContent = new PageContent();
                printContent.Child = printPage;
                printDoc.Pages.Add(printContent);
            }
            diag.PrintDocument(printDoc.DocumentPaginator, "Print1");
        }

        private void MenuTiffExport_Click(object sender, RoutedEventArgs e)
        {
            if (pdfPages1.Count < 1) return;
            if (pdfPages2.Count < 1) return;

            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.DefaultExt = ".tif";
            dialog.Filter = "TIFF files (.tif)|*.tif";
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string filename = dialog.FileName;
                using (var fs = new FileStream(filename, FileMode.Create))
                {
                    var encoder = new TiffBitmapEncoder();


                    int pageNum = Math.Min((int)pdfPages1.Count, (int)pdfPages2.Count);
                    for (int i = 0; i < pageNum; i++)
                    {
                        Mat modifiedMat1 = new Mat();
                        Mat modifiedMat2 = new Mat();
                        OpenCvSharp.Size pageSize1 = pdfPages1[i].Size();
                        OpenCvSharp.Size pageSize2 = pdfPages2[i].Size();
                        if ((pageSize1.Height != pageSize2.Height) || (pageSize1.Width != pageSize2.Width))
                        {
                            int biggerHeight = Math.Max(pageSize1.Height, pageSize2.Height);
                            int biggerWidth = Math.Max(pageSize1.Width, pageSize2.Width);
                            OpenCvSharp.Size adjustedSize = new OpenCvSharp.Size();
                            adjustedSize.Height = biggerHeight;
                            adjustedSize.Width = biggerWidth;
                            Cv2.Resize(pdfPages1[i], modifiedMat1, adjustedSize);
                            Cv2.Resize(pdfPages2[i], modifiedMat2, adjustedSize);
                        }
                        else
                        {
                            modifiedMat1 = pdfPages1[i];
                            modifiedMat2 = pdfPages2[i];
                        }

                        Mat msk = new Mat();
                        Cv2.BitwiseAnd(modifiedMat1, modifiedMat2, msk);

                        Mat m = new Mat();
                        Cv2.Merge(new Mat[] { modifiedMat2, msk, modifiedMat1 }, m);

                        BitmapSource img = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m);
                        encoder.Frames.Add(BitmapFrame.Create(img));
                    }
                    encoder.Save(fs);
                }
            }
        }

        private void MenuPNGExport_Click(object sender, RoutedEventArgs e)
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

                    Mat modifiedMat1 = new Mat();
                    Mat modifiedMat2 = new Mat();
                    OpenCvSharp.Size pageSize1 = pdfPages1[displayedPageNumber - 1].Size();
                    OpenCvSharp.Size pageSize2 = pdfPages2[displayedPageNumber - 1].Size();
                    if ((pageSize1.Height != pageSize2.Height) || (pageSize1.Width != pageSize2.Width))
                    {
                        int biggerHeight = Math.Max(pageSize1.Height, pageSize2.Height);
                        int biggerWidth = Math.Max(pageSize1.Width, pageSize2.Width);
                        OpenCvSharp.Size adjustedSize = new OpenCvSharp.Size();
                        adjustedSize.Height = biggerHeight;
                        adjustedSize.Width = biggerWidth;
                        Cv2.Resize(pdfPages1[displayedPageNumber - 1], modifiedMat1, adjustedSize);
                        Cv2.Resize(pdfPages2[displayedPageNumber - 1], modifiedMat2, adjustedSize);
                    }
                    else
                    {
                        modifiedMat1 = pdfPages1[displayedPageNumber - 1];
                        modifiedMat2 = pdfPages2[displayedPageNumber - 1];
                    }

                    Mat msk = new Mat();
                    Cv2.BitwiseAnd(modifiedMat1, modifiedMat2, msk);

                    Mat m = new Mat();
                    Cv2.Merge(new Mat[] { modifiedMat2, msk, modifiedMat1 }, m);

                    BitmapSource img = WriteableBitmapConverter.ToWriteableBitmap(m);
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(fs);
                }
            }
        }

        private void ScvMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (scvMain.IsMouseCaptureWithin) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ScrollViewer sc = (ScrollViewer)sender;
                System.Windows.Point pt = e.GetPosition((ScrollViewer)sender);
                if (chkMove.IsChecked == false)
                {
                    sc.ScrollToVerticalOffset(startPosition.Y + (pt.Y - startPoint.Y) * -1);
                    sc.ScrollToHorizontalOffset(startPosition.X + (pt.X - startPoint.X) * -1);

                    txtPointerInfo.Text = "Mouse LeftButtonDown\n X :" + startPoint.X.ToString() + "\n Y :" + startPoint.Y.ToString();
                    txtPointerInfo.Text = txtPointerInfo.Text + "\nMouseMove\n X : " + pt.X.ToString() + "\n Y : " + pt.Y.ToString();
                }
                else
                {
                    return;
                }
            }
        }

        private void ScvMain_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition((ScrollViewer)sender);
            ScrollViewer sc = (ScrollViewer)sender;
            startPosition = new System.Windows.Point(sc.HorizontalOffset, sc.VerticalOffset);

            txtPointerInfo.Text = "Mouse LeftButtonDown\n X :" + startPoint.X.ToString() + "\n Y :" + startPoint.Y.ToString();

            txtPointerInfo.Text = imgSub.GetValue(Canvas.TopProperty).ToString() + ":" + imgSub.GetValue(Canvas.LeftProperty).ToString();

        }

        private void CvsMain_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            canvasStartPoint = e.GetPosition((Canvas)sender);

            txtPointerInfo.Text = "Canvas Mouse LeftButtonDown\n X :" + canvasStartPoint.X.ToString() + "\n Y :" + canvasStartPoint.Y.ToString();
        }

        private void CvsMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Canvas sc = (Canvas)sender;
                System.Windows.Point pt = e.GetPosition((Canvas)sender);
                if (chkMove.IsChecked == true)
                {
                    //System.Windows.Window mainWindow = System.Windows.Application.Current.MainWindow;
                    //PresentationSource mainWindowPresentationSource = PresentationSource.FromVisual(mainWindow);
                    //Matrix m = mainWindowPresentationSource.CompositionTarget.TransformFromDevice;
                    //double dpiWidthFactor = m.M11;
                    //double dpiHeightFactor = m.M22;

                    double currentTop = (double)imgSub.GetValue(Canvas.TopProperty);
                    double currentLeft = (double)imgSub.GetValue(Canvas.LeftProperty);

                    double changedTop = currentTop + pt.Y - canvasStartPoint.Y;
                    double changedLeft = currentLeft + pt.X - canvasStartPoint.X;

                    imgSub.SetValue(Canvas.TopProperty, changedTop);
                    imgSub.SetValue(Canvas.LeftProperty, changedLeft);

                    canvasStartPoint = pt;
                }
                else
                {
                    return;
                }
            }

        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e != null)
            {
                double sizeRatio = e.NewValue / 100;
                Matrix mx = new Matrix();
                mx.Scale(sizeRatio, sizeRatio);
                //imgSub.SetValue(Canvas.TopProperty, (double)imgSub.GetValue(Canvas.TopProperty) * sizeRatio);
                //imgSub.SetValue(Canvas.LeftProperty, (double)imgSub.GetValue(Canvas.LeftProperty) * sizeRatio);
                imgMain.LayoutTransform = new MatrixTransform(mx);
                imgSub.LayoutTransform = new MatrixTransform(mx);
                cvsMain.Height = imgMain.ActualHeight * sizeRatio;
                cvsMain.Width = imgMain.ActualWidth * sizeRatio;
            }
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
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
            displayedPageNumber = 1;
            ShowPage(displayedPageNumber, displayedPageNumber);

            chkMove.IsEnabled = true;
            chkResize.IsEnabled = true;
            chkLineThickness.IsEnabled = true;

        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
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

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
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

        private void ChkMove_Checked(object sender, RoutedEventArgs e)
        {
            sldScale.IsEnabled = false;
            btnPrev.IsEnabled = false;
            btnNext.IsEnabled = false;
            btnFixPosition.IsEnabled = true;
            chkResize.IsEnabled = false;
            chkLineThickness.IsEnabled = false;

            int pageNumber1 = displayedPageNumber;
            int pageNumber2 = displayedPageNumber;
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

            Mat m1 = new Mat();
            Mat msk1 = new Mat(modifiedMat1.Size(),modifiedMat1.Type(), OpenCvSharp.Scalar.All(255));
            Cv2.Merge(new Mat[] { msk1, modifiedMat1, modifiedMat1 }, m1);

            BitmapSource img1 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m1);
            imgMain.Source = img1;

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            Mat m2 = new Mat();
            Mat msk2 = new Mat(modifiedMat2.Size(), modifiedMat2.Type(), OpenCvSharp.Scalar.All(255));
            Mat m3 = new Mat();
            Cv2.BitwiseNot(modifiedMat2, m3);
            Cv2.Merge(new Mat[] { modifiedMat2, modifiedMat2, msk2, m3}, m2);

            BitmapSource img2 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m2);
            imgSub.Source = img2;
            imgSub.LayoutTransform = new MatrixTransform(mx);

        }

        private void ChkMove_Unchecked(object sender, RoutedEventArgs e)
        {

            ShowPage(displayedPageNumber, displayedPageNumber);

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            imgSub.SetValue(Canvas.TopProperty,(double)0);
            imgSub.SetValue(Canvas.LeftProperty,(double)0);

            imgSub.Source = null;

            sldScale.IsEnabled = true;
            btnPrev.IsEnabled = true;
            btnNext.IsEnabled = true;
            btnFixPosition.IsEnabled = false;
            chkResize.IsEnabled = true;
            chkLineThickness.IsEnabled = true;
        }

        private void BtnFixPosition_Click(object sender, RoutedEventArgs e)
        {
            double displayScale = sldScale.Value / 100;
            double img2PosY = (double)imgSub.GetValue(Canvas.TopProperty) / displayScale;
            double img2PosX = (double)imgSub.GetValue(Canvas.LeftProperty) / displayScale;

            Mat modifiedMat1 = new Mat();
            Mat modifiedMat2 = new Mat();
            double left1 = 0;
            double left2 = 0;
            double right1 = 0;
            double right2 = 0;
            double top1 = 0;
            double top2 = 0;
            double bottom1 = 0;
            double bottom2 = 0;
            if (img2PosX < 0)
            {
                left1 = img2PosX * -1;
                left2 = 0;
                right1 = 0;
                right2 = img2PosX * -1;
            }
            else
            {
                left1 = 0;
                left2 = img2PosX;
                right1 = img2PosX;
                right2 = 0;
            }
            if (img2PosY < 0)
            {
                top1 = img2PosY * -1;
                top2 = 0;
                bottom1 = 0;
                bottom2 = img2PosY * -1;
            }
            else
            {
                top1 = 0;
                top2 = img2PosY;
                bottom1 = img2PosY;
                bottom2 = 0;
            }
            modifiedMat1 = pdfPages1[displayedPageNumber - 1].CopyMakeBorder((int)top1, (int)bottom1, (int)left1, (int)right1, BorderTypes.Constant, 255);
            modifiedMat2 = pdfPages2[displayedPageNumber - 1].CopyMakeBorder((int)top2, (int)bottom2, (int)left2, (int)right2, BorderTypes.Constant, 255);
            pdfPages1[displayedPageNumber - 1] = modifiedMat1;
            pdfPages2[displayedPageNumber - 1] = modifiedMat2;

            chkMove.IsChecked = false;
            chkResize.IsEnabled = true;
            chkLineThickness.IsEnabled = true;
            ShowPage(displayedPageNumber, displayedPageNumber);
        }

        private void ChkResize_Checked(object sender, RoutedEventArgs e)
        {
            sldScale.IsEnabled = false;
            btnPrev.IsEnabled = false;
            btnNext.IsEnabled = false;
            btnFixPosition.IsEnabled = false;
            txtResize.IsEnabled = true;
            btnFixResize.IsEnabled = false;
            chkMove.IsEnabled = false;
            chkLineThickness.IsEnabled = false;
        }

        private void ChkResize_Unchecked(object sender, RoutedEventArgs e)
        {
            ShowPage(displayedPageNumber, displayedPageNumber);

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            imgSub.SetValue(Canvas.TopProperty, (double)0);
            imgSub.SetValue(Canvas.LeftProperty, (double)0);

            imgSub.Source = null;

            sldScale.IsEnabled = true;
            btnPrev.IsEnabled = true;
            btnNext.IsEnabled = true;
            chkMove.IsEnabled = true;
            btnFixPosition.IsEnabled = false;
            txtResize.IsEnabled = false;
            btnFixResize.IsEnabled = false;
            chkLineThickness.IsEnabled = true;
        }

        private void TxtResize_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return) return;
            int targetRatio = 100;
            bool result = int.TryParse(txtResize.Text, out targetRatio);
            if (result != true)
            {
                txtPointerInfo.Text = "自然数を入力してください。";
                return;
            }
            if (targetRatio < 50 || targetRatio > 150)
            {
                txtPointerInfo.Text = "50～150の数値を入力してください。";
                return;
            }
            btnFixResize.IsEnabled = true;
            txtPointerInfo.Text = targetRatio.ToString();

            int pageNumber1 = displayedPageNumber;
            int pageNumber2 = displayedPageNumber;
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

            Mat m1 = new Mat();
            Mat msk1 = new Mat(modifiedMat1.Size(), modifiedMat1.Type(), OpenCvSharp.Scalar.All(255));
            Cv2.Merge(new Mat[] { msk1, modifiedMat1, modifiedMat1 }, m1);

            BitmapSource img1 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m1);
            imgMain.Source = img1;

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            Mat m2 = new Mat();
            Mat msk2 = new Mat(modifiedMat2.Size(), modifiedMat2.Type(), OpenCvSharp.Scalar.All(255));
            Mat m3 = new Mat();
            Cv2.BitwiseNot(modifiedMat2, m3);
            Cv2.Merge(new Mat[] { modifiedMat2, modifiedMat2, msk2, m3 }, m2);

            var m4 = new Mat();
            Cv2.Resize(m2, m4, new OpenCvSharp.Size(), targetRatio / 100.0, targetRatio / 100.0);

            BitmapSource img2 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m4);
            imgSub.Source = img2;
            imgSub.LayoutTransform = new MatrixTransform(mx);

            display1 = m1;
            display2 = m4;
        }

        private void BtnFixResize_Click(object sender, RoutedEventArgs e)
        {
            OpenCvSharp.Size s1 = display1.Size();
            OpenCvSharp.Size s2 = display2.Size();

            double lengthWidth = Math.Abs(s1.Width - s2.Width);
            double lengthHeight = Math.Abs(s1.Height - s2.Height);

            if (s1.Width != s2.Width)
            {
                var resizedMat = new Mat();
                resizedMat = pdfPages2[displayedPageNumber-1].CopyMakeBorder(0, (int)lengthHeight, 0, (int)lengthWidth, BorderTypes.Constant, 255);
                pdfPages2[displayedPageNumber - 1] = resizedMat;
            }

            ShowPage(displayedPageNumber, displayedPageNumber);

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            imgSub.SetValue(Canvas.TopProperty, (double)0);
            imgSub.SetValue(Canvas.LeftProperty, (double)0);

            imgSub.Source = null;

            sldScale.IsEnabled = true;
            btnPrev.IsEnabled = true;
            btnNext.IsEnabled = true;
            chkMove.IsEnabled = true;
            btnFixPosition.IsEnabled = false;
            chkResize.IsChecked = false;
            txtResize.IsEnabled = false;
            btnFixResize.IsEnabled = false;
            chkLineThickness.IsEnabled = true;
        }

        private void ChkLineThickness_Checked(object sender, RoutedEventArgs e)
        {
            chkMove.IsEnabled = false;
            chkResize.IsEnabled = false;
            btnThickness1.IsEnabled = true;
            btnThickness2.IsEnabled = true;
            btnFixThickness.IsEnabled = true;
        }

        private void ChkLineThickness_Unchecked(object sender, RoutedEventArgs e)
        {
            ShowPage(displayedPageNumber, displayedPageNumber);

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            imgSub.SetValue(Canvas.TopProperty, (double)0);
            imgSub.SetValue(Canvas.LeftProperty, (double)0);

            imgSub.Source = null;
            thickness1 = null;
            thickness2 = null;

            chkMove.IsEnabled = true;
            chkResize.IsEnabled = true;
            btnThickness1.IsEnabled = false;
            btnThickness2.IsEnabled = false;
            btnFixThickness.IsEnabled = false;
        }

        private void BtnThickness_Click(object sender, RoutedEventArgs e)
        {
            int pageNumber1 = displayedPageNumber;
            int pageNumber2 = displayedPageNumber;
            // ページ数が表示の条件に合わない場合は、何もしない。
            if (pageNumber1 < 1) return;
            if (pdfPages1.Count == 0) return;
            if (pageNumber1 > pdfPages1.Count) return;
            if (pdfPages2.Count < pageNumber2) return;

            // 膨張させる元のデータを選択する。
            Mat modifiedMat1 = new Mat();
            Mat modifiedMat2 = new Mat();
            if (thickness1 == null || thickness2 == null)
            {
                List<Mat> mats = AdjustMatSize(pdfPages1[pageNumber1 - 1], pdfPages2[pageNumber2 - 1]);
                modifiedMat1 = mats[0].Clone();
                modifiedMat2 = mats[1].Clone();
            }
            else
            {
                modifiedMat1 = thickness1.Clone();
                modifiedMat2 = thickness2.Clone();
            }

            // 線を膨張させる。
            string selectedFileTag = ((Button)sender).Tag.ToString();
            if (selectedFileTag == "1")
            {
                Cv2.Erode(modifiedMat1, modifiedMat1, new Mat(new OpenCvSharp.Size(3, 3), MatType.CV_8UC1));
            }
            else
            {
                Cv2.Erode(modifiedMat2, modifiedMat2, new Mat(new OpenCvSharp.Size(3, 3), MatType.CV_8UC1));
            }

            Mat m1 = new Mat();
            Mat msk1 = new Mat(modifiedMat1.Size(), modifiedMat1.Type(), OpenCvSharp.Scalar.All(255));
            Cv2.Merge(new Mat[] { msk1, modifiedMat1, modifiedMat1 }, m1);

            BitmapSource img1 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m1);
            imgMain.Source = img1;

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            Mat m2 = new Mat();
            Mat msk2 = new Mat(modifiedMat2.Size(), modifiedMat2.Type(), OpenCvSharp.Scalar.All(255));
            Mat m3 = new Mat();
            Cv2.BitwiseNot(modifiedMat2, m3);
            Cv2.Merge(new Mat[] { modifiedMat2, modifiedMat2, msk2, m3 }, m2);

            BitmapSource img2 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m2);
            imgSub.Source = img2;
            imgSub.LayoutTransform = new MatrixTransform(mx);

            thickness1 = modifiedMat1.Clone();
            thickness2 = modifiedMat2.Clone();
        }

        private void BtnFixThickness_Click(object sender, RoutedEventArgs e)
        {
            pdfPages1[displayedPageNumber - 1] = thickness1.Clone();
            pdfPages2[displayedPageNumber - 1] = thickness2.Clone();

            ShowPage(displayedPageNumber, displayedPageNumber);

            double sizeRatio = sldScale.Value / 100;
            Matrix mx = new Matrix();
            mx.Scale(sizeRatio, sizeRatio);
            imgMain.LayoutTransform = new MatrixTransform(mx);
            cvsMain.Height = imgMain.ActualHeight * sizeRatio;
            cvsMain.Width = imgMain.ActualWidth * sizeRatio;

            imgSub.SetValue(Canvas.TopProperty, (double)0);
            imgSub.SetValue(Canvas.LeftProperty, (double)0);

            imgSub.Source = null;
            thickness1 = null;
            thickness2 = null;

            sldScale.IsEnabled = true;
            btnPrev.IsEnabled = true;
            btnNext.IsEnabled = true;
            chkMove.IsEnabled = true;
            chkResize.IsEnabled = true;
            chkLineThickness.IsEnabled = true;
            chkLineThickness.IsChecked = false;
            btnPrev.IsEnabled = true;
            btnNext.IsEnabled = true;
            sldScale.IsEnabled = true;
            btnThickness1.IsEnabled = false;
            btnThickness2.IsEnabled = false;
            btnFixThickness.IsEnabled = false;
        }
    }
}
