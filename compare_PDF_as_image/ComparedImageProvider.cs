using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;


namespace compare_PDF_as_image
{
    class ComparedImageProvider
    {
        private string _filename1 = "";
        public string FileName1
        {
            get { return _filename1; }
            set { if (value.Length > 0) _filename1 = value; }
        }
        private string _filename2 = "";
        public string FileName2
        {
            get { return _filename2; }
            set { if (value.Length > 0) _filename2 = value; }
        }


        private List<Mat> _pdfPages1 = null;
        public List<Mat> PdfPages1
        {
            get { return _pdfPages1; }
            set { _pdfPages1 = value; }
        }
        private List<Mat> _pdfPages2 = null;
        public List<Mat> PdfPages2
        {
            get { return _pdfPages2; }
            set { _pdfPages2 = value; }
        }

        // 両方nullのとき0、File1だけのとき1、File2だけのとき2、両方のデータがあるとき3
        public int RegisterdDocumentNumber
        {
            get
            {
                int i = 0;
                if (_pdfPages1 != null) i = i + 1;
                if (_pdfPages2 != null) i = i + 2;
                return i;
            }
        }

        private int _pageNum1 = 0;
        public int PageNum1
        {
            get { return _pageNum1; }
            set { _pageNum1 = value; }
        }
        private int _pageNum2 = 0;
        public int PageNum2
        {
            get { return _pageNum2; }
            set { _pageNum2 = value; }
        }

        private bool _emphasis = true;
        public bool Emphasis { set { _emphasis = value; } }
        
        public int EndPageNum1
        {
            get {
                if (_pdfPages1 != null)
                {
                    return _pdfPages1.Count;
                }
                return 0;
            }
        }
        public int EndPageNum2
        {
            get
            {
                if (_pdfPages2 != null)
                {
                    return _pdfPages2.Count;
                }
                return 0;
            }
        }
        public OpenCvSharp.Size PageSize1
        {
            get { return _pdfPages1[_pageNum1 - 1].Size(); }
        }
        public OpenCvSharp.Size PageSize2
        {
            get { return _pdfPages2[_pageNum2 - 1].Size(); }
        }

        public List<Mat> AdjustedMats
        {
            get
            {
                List<Mat> mats = AdjustMatSize(_pdfPages1[_pageNum1 - 1], _pdfPages2[_pageNum2 - 1]);

                var returnMats = new List<Mat>();
                returnMats.Add(mats[0]);
                returnMats.Add(mats[1]);
                return returnMats;
            }
        }

        public void ClearPages(int docID)
        {
            if (docID == 1)
            {
                if (_pdfPages1 != null) _pdfPages1.Clear();
            }
            else if (docID == 2)
            {
                if (_pdfPages2 != null) _pdfPages2.Clear();
            }

        }

        public void ChangePage(int docID, Mat mat)
        {
            if (docID == 1)
            {
                _pdfPages1[_pageNum1 - 1] = mat;
            } else if (docID == 2)
            {
                _pdfPages2[_pageNum2 - 1] = mat;
            }
        }

        public BitmapSource MergedPage
        {
            get
            {
                List<Mat> mats = AdjustMatSize(_pdfPages1[_pageNum1 - 1], _pdfPages2[_pageNum2 - 1]);

                Mat m = new Mat();
                if (_emphasis == false)
                {
                    // ２つのページの共通部分の画像を作る。
                    Mat msk = new Mat();
                    Cv2.BitwiseAnd(mats[0], mats[1], msk);
                    // 比較を表示するための画像を作る。
                    Cv2.Merge(new Mat[] { mats[0], msk, mats[1] }, m);
                }
                else
                {
                    // ２つのページの共通でない部分を抽出する
                    Mat emphasisShapeMat = new Mat();
                    Cv2.BitwiseXor(mats[0], mats[1], emphasisShapeMat);
                    // 共通でない部分の線を太くする（強調表示用）
                    Cv2.Dilate(emphasisShapeMat, emphasisShapeMat, new Mat(new OpenCvSharp.Size(10, 10), MatType.CV_8UC1), null, 20);
                    // 反転する
                    Cv2.BitwiseNot(emphasisShapeMat, emphasisShapeMat);
                    // 強調部分に色をつける
                    Mat whiteMat = new Mat(mats[0].Size(), mats[1].Type(), OpenCvSharp.Scalar.All(255));
                    var coloredEmphasisShapeMat = new Mat();
                    Cv2.Merge(new Mat[] { emphasisShapeMat, whiteMat, whiteMat }, coloredEmphasisShapeMat);

                    // ２つのページのいずれかに線がある部分を抽出する
                    var drawShapeMat = new Mat();
                    Cv2.BitwiseAnd(mats[0], mats[1], drawShapeMat);
                    var msk = new Mat();
                    Cv2.Threshold(drawShapeMat, msk, 127, 255, ThresholdTypes.Binary);
                    Cv2.BitwiseNot(drawShapeMat, drawShapeMat);

                    var maskedEmphasisMat = new Mat();
                    Cv2.BitwiseAnd(coloredEmphasisShapeMat, coloredEmphasisShapeMat, maskedEmphasisMat, msk);

                    // 両ページを合成した画像を作成する
                    Mat margedPage = new Mat();
                    Cv2.Merge(new Mat[] { mats[0], msk, mats[1] }, margedPage);

                    var maskedMargedPage = new Mat();
                    Cv2.BitwiseAnd(margedPage, margedPage, maskedMargedPage, drawShapeMat);

                    Cv2.Add(maskedEmphasisMat, maskedMargedPage, m);
                }
                //Cv2.ImShow("test", m);

                BitmapSource img = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(m);
                return img;
            }
        }

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
    }
}
