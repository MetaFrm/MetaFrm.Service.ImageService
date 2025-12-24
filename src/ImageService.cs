using MetaFrm.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Drawing;
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
                                HandleBarcodeGeneration(command.Values[i], outPutTableBarcodeImage, key, i);
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
            using var engine = GetEngine(language);
            using var img = Pix.LoadFromMemory(buffer);
            using var page = engine.Process(img);
            var text = page.GetText();

            Data.DataRow dataRow = new();
            dataRow.Values.Add("CommandName", new Data.DataValue(key));
            dataRow.Values.Add("RowIndex", new Data.DataValue(i));
            dataRow.Values.Add("Text", new Data.DataValue(text.Trim().ReplaceLineEndings()));
            outPutTableText.DataRows.Add(dataRow);
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static void HandleBarcodeGeneration(Dictionary<string, Data.DataValue> pairs, Data.DataTable outPutTableBarcodeImage, string key, int i)
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

            Bitmap qrCodeBitmap = writer.Write(text);


            if (noSpace == true)
            {
                Color c = qrCodeBitmap.GetPixel(0, 0);
                int xResult = 0;
                int yResult = 0;
                Point start;
                Point end;
                Size size = new(qrCodeBitmap.Width - 1, qrCodeBitmap.Height - 1);

                bool isOut = false;
                for (int x = 0; x < size.Width; x++)
                {
                    for (int y = 0; y < size.Height; y++)
                    {
                        if (c != qrCodeBitmap.GetPixel(x, y))
                        {
                            xResult = x;
                            isOut = true;
                            break;
                        }
                    }
                    if (isOut) break;
                }
                isOut = false;
                for (int y = 0; y < size.Height; y++)
                {
                    for (int x = 0; x < size.Width; x++)
                    {
                        if (c != qrCodeBitmap.GetPixel(x, y))
                        {
                            yResult = y;
                            isOut = true;
                            break;
                        }
                    }
                    if (isOut) break;
                }
                start = new Point(xResult, yResult);

                isOut = false;
                for (int x = size.Width; x >= 0; x--)
                {
                    for (int y = size.Height; y >= 0; y--)
                    {
                        if (c != qrCodeBitmap.GetPixel(x, y))
                        {
                            xResult = x;
                            isOut = true;
                            break;
                        }
                    }
                    if (isOut) break;
                }
                isOut = false;
                for (int y = size.Height; y >= 0; y--)
                {
                    for (int x = size.Width; x >= 0; x--)
                    {
                        if (c != qrCodeBitmap.GetPixel(x, y))
                        {
                            yResult = y;
                            isOut = true;
                            break;
                        }
                    }
                    if (isOut) break;
                }
                end = new Point(xResult, yResult);

                Bitmap target = new(end.X - start.X, end.Y - start.Y);

                using Graphics g = Graphics.FromImage(target);
                g.DrawImage(qrCodeBitmap, new Rectangle(0, 0, target.Width, target.Height), new Rectangle(start, target.Size), GraphicsUnit.Pixel);

                qrCodeBitmap = target;
            }

            using MemoryStream stream = new();
            qrCodeBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

            stream.Position = 0;

            Data.DataRow dataRow = new();
            dataRow.Values.Add("CommandName", new Data.DataValue(key));
            dataRow.Values.Add("RowIndex", new Data.DataValue(i));
            dataRow.Values.Add("BarcodeImage", new Data.DataValue(Convert.ToBase64String(stream.ToArray())));
            outPutTableBarcodeImage.DataRows.Add(dataRow);
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