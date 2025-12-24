using MetaFrm.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Datamatrix.Encoder;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace MetaFrm.Service
{
    /// <summary>
    /// 이미지 서비스를 구현합니다.
    /// </summary>
    public class ImageService : IService
    {
        private static readonly ConcurrentDictionary<string, TesseractEngine> _engines = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _engineLocks = new();

        static ImageService() { InitAsync(); }

        /// <summary>
        /// 이미지 서비스 생성자 입니다.
        /// </summary>
        public ImageService() { }


        /// <summary>
        /// https://github.com/tesseract-ocr/tessdata
        /// </summary>
        private static async void InitAsync()
        {
            string file;
            DirectoryInfo directoryInfo = new("tessdata");

            try
            {
                if (!directoryInfo.Exists)
                    directoryInfo.Create();
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "{Message}", ex.Message);
            }

            try
            {
                file = @"tessdata\eng.traineddata";

                if (!File.Exists(file))
                {
                    HttpResponseMessage response = Factory.HttpClientFactory.CreateClient().GetAsync("https://download.metafrm.net/Tesseract/tessdata/eng.traineddata").Result;
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync();
                        File.WriteAllBytes(file, data);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "{Message}", ex.Message);
            }

            try
            {
                file = @"tessdata\kor.traineddata";

                if (!File.Exists(file))
                {
                    HttpResponseMessage response = Factory.HttpClientFactory.CreateClient().GetAsync("https://download.metafrm.net/Tesseract/tessdata/kor.traineddata").Result;
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync();
                        File.WriteAllBytes(file, data);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "{Message}", ex.Message);
            }

            try
            {
                file = @"tessdata\kor_vert.traineddata";

                if (!File.Exists(file))
                {
                    HttpResponseMessage response = Factory.HttpClientFactory.CreateClient().GetAsync("https://download.metafrm.net/Tesseract/tessdata/kor_vert.traineddata").Result;
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync();
                        File.WriteAllBytes(file, data);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "{Message}", ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        Response IService.Request(ServiceData serviceData)
        {
            Response response;
            byte[] buffer;

            try
            {
                if (serviceData.ServiceName == null || !serviceData.ServiceName.Equals("MetaFrm.Service.ImageService"))
                    throw new Exception("Not MetaFrm.Service.ImageService");

                response = new() { DataSet = new Data.DataSet() };

                Data.DataTable outPutTableBarcode;
                outPutTableBarcode = new Data.DataTable("Barcode");
                outPutTableBarcode.DataColumns.Add(new Data.DataColumn("CommandName", "System.String"));
                outPutTableBarcode.DataColumns.Add(new Data.DataColumn("RowIndex", "System.Int32"));
                outPutTableBarcode.DataColumns.Add(new Data.DataColumn("Barcode", "System.String"));
                outPutTableBarcode.DataColumns.Add(new Data.DataColumn("BarcodeFormat", "System.String"));
                outPutTableBarcode.DataColumns.Add(new Data.DataColumn("NumBits", "System.Int32"));

                Data.DataTable outPutTableBarcodeImage;
                outPutTableBarcodeImage = new Data.DataTable("BarcodeImage");
                outPutTableBarcodeImage.DataColumns.Add(new Data.DataColumn("CommandName", "System.String"));
                outPutTableBarcodeImage.DataColumns.Add(new Data.DataColumn("RowIndex", "System.Int32"));
                outPutTableBarcodeImage.DataColumns.Add(new Data.DataColumn("BarcodeImage", "System.String"));

                Data.DataTable outPutTableText;
                outPutTableText = new Data.DataTable("Text");
                outPutTableText.DataColumns.Add(new Data.DataColumn("CommandName", "System.String"));
                outPutTableText.DataColumns.Add(new Data.DataColumn("RowIndex", "System.Int32"));
                outPutTableText.DataColumns.Add(new Data.DataColumn("Text", "System.String"));

                BarcodeReader reader = new()
                {
                    AutoRotate = true,
                    Options = new DecodingOptions { TryHarder = true }
                };

                foreach (var key in serviceData.Commands.Keys)
                {
                    Command command = serviceData.Commands[key];

                    for (int i = 0; i < command.Values.Count; i++)
                    {
                        string[]? commandValue = command.Values[i]["Command"].StringValue?.ToLower().Split(",");
                        string? imageValue = command.Values[i]["Image"].StringValue;
                        int seperateCount = command.Values[i].TryGetValue("Seperate", out Data.DataValue? dataValue1) ? dataValue1.IntValue ?? 4 : 4;
                        string? language = command.Values[i].TryGetValue("Language", out Data.DataValue? dataValue2) ? dataValue2.StringValue : "kor";

                        if (commandValue == null)
                            continue;

                        if (imageValue != null)
                        {
                            buffer = Convert.FromBase64String(imageValue);

                            //바코드 인식
                            if (commandValue.Contains("barcode"))
                                ProcessBarcodeRecognition(buffer, reader, outPutTableBarcode, seperateCount, key, i);

                            //문자 인식
                            if (commandValue.Contains("text"))
                                ProcessTextRecognition(language ?? "kor", buffer, outPutTableText, key, i);
                        }
                        else
                        {
                            //바코드 생성
                            if (commandValue.Contains("barcodeimage"))
                                ProcessBarcodeGeneration(command.Values[i], outPutTableBarcodeImage, key, i);
                        }
                    }

                }

                if (outPutTableBarcode.DataRows.Count > 0)
                    response.DataSet.DataTables.Add(outPutTableBarcode);
                if (outPutTableText.DataRows.Count > 0)
                    response.DataSet.DataTables.Add(outPutTableText);
                if (outPutTableBarcodeImage.DataRows.Count > 0)
                    response.DataSet.DataTables.Add(outPutTableBarcodeImage);

                response.Status = Status.OK;

            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "{Message}", ex.Message);
                return new Response(ex);
            }


            return response;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static void ProcessBarcodeRecognition(byte[] buffer, BarcodeReader reader, Data.DataTable outPutTableBarcode, int seperateCount, string key, int i)
        {
            Result[] result;

            using MemoryStream ms = new(buffer);
            using Image image = Image.FromStream(ms);
            using Bitmap bitmap = new(image);

            result = reader.DecodeMultiple(bitmap);

            List<string> strings = [];

            if (result != null && result.Length > 0)
            {
                foreach (var barcode in result)
                {
                    if (!strings.Contains(barcode.Text))
                    {
                        strings.Add(barcode.Text);

                        Data.DataRow dataRow = new();
                        dataRow.Values.Add("CommandName", new Data.DataValue(key));
                        dataRow.Values.Add("RowIndex", new Data.DataValue(i));
                        dataRow.Values.Add("Barcode", new Data.DataValue(barcode.Text));
                        dataRow.Values.Add("BarcodeFormat", new Data.DataValue(barcode.BarcodeFormat.ToString()));
                        dataRow.Values.Add("NumBits", new Data.DataValue(barcode.NumBits));
                        outPutTableBarcode.DataRows.Add(dataRow);
                    }
                }
            }
            else
            {
                //이미지를 분할 해서 인식
                List<Bitmap> bitmaps = BitmapSeperate(bitmap, seperateCount);

                foreach (var item in bitmaps)
                {
                    using (item)
                    {
                        result = reader.DecodeMultiple(item);

                        if (result != null && result.Length > 0)
                        {
                            foreach (var barcode in result)
                            {
                                if (!strings.Contains(barcode.Text))
                                {
                                    strings.Add(barcode.Text);

                                    Data.DataRow dataRow = new();
                                    dataRow.Values.Add("CommandName", new Data.DataValue(key));
                                    dataRow.Values.Add("RowIndex", new Data.DataValue(i));
                                    dataRow.Values.Add("Barcode", new Data.DataValue(barcode.Text));
                                    dataRow.Values.Add("BarcodeFormat", new Data.DataValue(barcode.BarcodeFormat.ToString()));
                                    dataRow.Values.Add("NumBits", new Data.DataValue(barcode.NumBits));
                                    outPutTableBarcode.DataRows.Add(dataRow);
                                }
                            }
                        }
                    }
                }
            }
        }
        private static void ProcessTextRecognition(string language, byte[] buffer, Data.DataTable outPutTableText, string key, int i)
        {
            string text;
            using var engine = GetEngine(language);

            var sem = _engineLocks.GetOrAdd(language, _ => new SemaphoreSlim(1, 1));

            sem.Wait();

            try
            {
                using var img = Pix.LoadFromMemory(buffer);
                using var page = engine.Process(img);
                text = page.GetText();
            }
            finally
            {
                sem.Release();
            }

            Data.DataRow dataRow = new();
            dataRow.Values.Add("CommandName", new Data.DataValue(key));
            dataRow.Values.Add("RowIndex", new Data.DataValue(i));
            dataRow.Values.Add("Text", new Data.DataValue(text.Trim().ReplaceLineEndings()));
            outPutTableText.DataRows.Add(dataRow);
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static void ProcessBarcodeGeneration(Dictionary<string, Data.DataValue> pairs, Data.DataTable outPutTableBarcodeImage, string key, int i)
        {
            string? text = pairs["Text"].StringValue;
            string? characterSet = pairs.TryGetValue("CharacterSet", out Data.DataValue? valueCharacterSet) ? valueCharacterSet.StringValue : "UTF-8";
            string? barcodeFormat1 = pairs.TryGetValue("BarcodeFormat", out Data.DataValue? valueBarcodeFormat) ? valueBarcodeFormat.StringValue : "QR_CODE";
            int? widthValue = pairs.TryGetValue("Width", out Data.DataValue? valueWidth) ? valueWidth.IntValue : 300;
            int? heightValue = pairs.TryGetValue("Height", out Data.DataValue? valueHeight) ? valueHeight.IntValue : 300;
            bool? disableECI = pairs.TryGetValue("DisableECI", out Data.DataValue? valueDisableECI) ? valueDisableECI.BooleanValue : false;
            bool? pureBarcode = pairs.TryGetValue("PureBarcode", out Data.DataValue? valuePureBarcode) ? valuePureBarcode.BooleanValue : false;
            bool? noSpace = pairs.TryGetValue("NoSpace", out Data.DataValue? valueNoSpace) ? valueNoSpace.BooleanValue : false;

            if (text == null || characterSet == null || barcodeFormat1 == null || widthValue == null || heightValue == null)
                return;

            BarcodeFormat barcodeFormat = barcodeFormat1.EnumParse<BarcodeFormat>();
            EncodingOptions options;

            switch (barcodeFormat)
            {
                case BarcodeFormat.DATA_MATRIX:
                    string? symbolShape = pairs["DatamatrixSymbolShape"].StringValue;

                    options = new DatamatrixEncodingOptions()
                    {
                        CharacterSet = characterSet,//"UTF-8"
                        Width = (int)widthValue,
                        Height = (int)heightValue,
                        PureBarcode = pureBarcode ?? false,
                        SymbolShape = (symbolShape ?? "FORCE_SQUARE").EnumParse<SymbolShapeHint>(),
                    };
                    break;

                default:
                    options = new QrCodeEncodingOptions()
                    {
                        DisableECI = disableECI ?? false,
                        CharacterSet = characterSet,//"UTF-8"
                        Width = (int)widthValue,
                        Height = (int)heightValue,
                        PureBarcode = pureBarcode ?? false,
                    };
                    break;
            }

            BarcodeWriter writer = new()
            {
                Format = barcodeFormat1.EnumParse<BarcodeFormat>(),//QR_CODE
                Options = options
            };

            using  Bitmap original = writer.Write(text);
            Bitmap qrCodeBitmap = original;

            if (noSpace == true)
            {
                Rectangle bounds = GetQrContentBounds(original);

                if (bounds.Width > 0 && bounds.Height > 0 &&
                    (bounds.Width != original.Width || bounds.Height != original.Height))
                {
                    Bitmap target = new(bounds.Width, bounds.Height);

                    using (Graphics g = Graphics.FromImage(target))
                    {
                        g.DrawImage(
                            original,
                            new Rectangle(0, 0, target.Width, target.Height),
                            bounds,
                            GraphicsUnit.Pixel
                        );
                    }

                    qrCodeBitmap = target;
                }
            }

            using MemoryStream stream = new();
            qrCodeBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

            if (!ReferenceEquals(qrCodeBitmap, original))
                qrCodeBitmap.Dispose();

            stream.Position = 0;

            Data.DataRow dataRow = new();
            dataRow.Values.Add("CommandName", new Data.DataValue(key));
            dataRow.Values.Add("RowIndex", new Data.DataValue(i));
            dataRow.Values.Add("BarcodeImage", new Data.DataValue(Convert.ToBase64String(stream.ToArray())));
            outPutTableBarcodeImage.DataRows.Add(dataRow);
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416")]
        private static Rectangle GetQrContentBounds(Bitmap bitmap)
        {
            Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(
                rect,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb
            );

            try
            {
                int minX = bitmap.Width;
                int minY = bitmap.Height;
                int maxX = 0;
                int maxY = 0;

                unsafe
                {
                    byte* scan0 = (byte*)data.Scan0;
                    int stride = data.Stride;

                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        byte* row = scan0 + (y * stride);
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            byte b = row[x * 4];
                            byte g = row[x * 4 + 1];
                            byte r = row[x * 4 + 2];

                            // 흰색이 아니면 QR 코드 영역
                            if (!(r > 250 && g > 250 && b > 250))
                            {
                                if (x < minX) minX = x;
                                if (y < minY) minY = y;
                                if (x > maxX) maxX = x;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }
                }

                if (minX > maxX || minY > maxY)
                    return rect;

                return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static List<Bitmap> BitmapSeperate(Bitmap bitmapOrg, int seperateCount)
        {
            List<Bitmap> bitmaps = [];
            int height;
            int width;

            if (bitmapOrg.Height >= seperateCount * 20)
            {
                height = bitmapOrg.Height / seperateCount;

                for (int i = 0; i < seperateCount; i++)
                {
                    Rectangle cropRect = new(0, i * height, bitmapOrg.Width, height);

                    Bitmap target = new(cropRect.Width, cropRect.Height);

                    using Graphics g = Graphics.FromImage(target);

                    g.DrawImage(bitmapOrg, destRect: new Rectangle(0, 0, target.Width, target.Height), cropRect, GraphicsUnit.Pixel);

                    bitmaps.Add(target);
                }
            }

            if (bitmapOrg.Width >= seperateCount * 20)
            {
                width = bitmapOrg.Width / seperateCount;
                for (int i = 0; i < seperateCount; i++)
                {
                    Rectangle cropRect = new(i * width, 0, bitmapOrg.Height, width);

                    Bitmap target = new(cropRect.Width, cropRect.Height);

                    using Graphics g = Graphics.FromImage(target);

                    g.DrawImage(bitmapOrg, destRect: new Rectangle(0, 0, target.Width, target.Height), cropRect, GraphicsUnit.Pixel);

                    bitmaps.Add(target);
                }
            }

            return bitmaps;
        }

        private static TesseractEngine GetEngine(string lang)
        {
            lock (_engines)
                return _engines.GetOrAdd(lang, l => new TesseractEngine("./tessdata", l, EngineMode.Default));
        }
    }
}