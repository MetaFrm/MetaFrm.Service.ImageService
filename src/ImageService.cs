using MetaFrm.Diagnostics;
using MetaFrm.Extensions;
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
                Console.WriteLine(ex.Message);
            }

            try
            {
                file = @"tessdata\eng.traineddata";

                if (!File.Exists(file))
                    using (HttpClient client = new())
                    {
                        HttpResponseMessage response = client.GetAsync("https://download.metafrm.net/Tesseract/tessdata/eng.traineddata").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            File.WriteAllBytes(file, data);
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                file = @"tessdata\kor.traineddata";

                if (!File.Exists(file))
                    using (HttpClient client = new())
                    {
                        HttpResponseMessage response = client.GetAsync("https://download.metafrm.net/Tesseract/tessdata/kor.traineddata").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            File.WriteAllBytes(file, data);
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                file = @"tessdata\kor_vert.traineddata";

                if (!File.Exists(file))
                    using (HttpClient client = new())
                    {
                        HttpResponseMessage response = client.GetAsync("https://download.metafrm.net/Tesseract/tessdata/kor_vert.traineddata").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            File.WriteAllBytes(file, data);
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        Response IService.Request(ServiceData serviceData)
        {
            Response response;
            byte[] buffer;
            Point start = new();
            Point end = new();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

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

                BarcodeReader reader = new();

                foreach (var key in serviceData.Commands.Keys)
                {
                    for (int i = 0; i < serviceData.Commands[key].Values.Count; i++)
                    {
                        string[]? command = serviceData.Commands[key].Values[i]["Command"].StringValue?.ToLower().Split(",");
                        string? imageValue = serviceData.Commands[key].Values[i]["Image"].StringValue;
                        int seperateCount = serviceData.Commands[key].Values[i].TryGetValue("Seperate", out Data.DataValue? dataValue1) ? dataValue1.IntValue ?? 4 : 4;
                        string? language = serviceData.Commands[key].Values[i].TryGetValue("Language", out Data.DataValue? dataValue2) ? dataValue2.StringValue : "kor";

                        Result[] result;

                        if (command == null)
                            continue;

                        if (imageValue != null)
                        {
                            buffer = Convert.FromBase64String(imageValue);

                            //바코드 인식
                            if (command.Contains("barcode"))
                            {
                                using MemoryStream ms = new(buffer);

                                Bitmap bitmap = new(Image.FromStream(ms));

                                result = reader.DecodeMultiple(bitmap);

                                List<string> strings = new();

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

                            //문자 인식
                            if (command.Contains("barcode"))
                            {
                                using var engine = new TesseractEngine("./tessdata", language ?? "kor", EngineMode.Default);
                                using var img = Pix.LoadFromMemory(buffer);
                                using var page = engine.Process(img);
                                var text = page.GetText();

                                Data.DataRow dataRow = new();
                                dataRow.Values.Add("CommandName", new Data.DataValue(key));
                                dataRow.Values.Add("RowIndex", new Data.DataValue(i));
                                dataRow.Values.Add("Text", new Data.DataValue(text.Trim().ReplaceLineEndings()));
                                outPutTableText.DataRows.Add(dataRow);
                            }
                        }
                        else
                        {
                            //바코드 생성
                            if (command.Contains("barcodeimage"))
                            {
                                string? textValue = serviceData.Commands[key].Values[i]["Text"].StringValue;
                                string? characterSetValue = serviceData.Commands[key].Values[i]["CharacterSet"].StringValue;
                                string? barcodeFormatSetValue = serviceData.Commands[key].Values[i]["BarcodeFormat"].StringValue;
                                int? widthValue = serviceData.Commands[key].Values[i]["Width"].IntValue;
                                int? heightValue = serviceData.Commands[key].Values[i]["Height"].IntValue;
                                bool? disableECI = serviceData.Commands[key].Values[i].TryGetValue("DisableECI", out Data.DataValue? valueDisableECI) ? valueDisableECI.BooleanValue : false;
                                bool? pureBarcode = serviceData.Commands[key].Values[i].TryGetValue("PureBarcode", out Data.DataValue? valuePureBarcode) ? valuePureBarcode.BooleanValue : false;
                                bool? noSpace = serviceData.Commands[key].Values[i].TryGetValue("NoSpace", out Data.DataValue? valueNoSpace) ? valueNoSpace.BooleanValue : false;

                                if (textValue == null || characterSetValue == null || barcodeFormatSetValue == null || widthValue == null || heightValue == null)
                                    continue;

                                BarcodeFormat barcodeFormat = barcodeFormatSetValue.EnumParse<BarcodeFormat>();
                                EncodingOptions options;

                                switch (barcodeFormat)
                                {
                                    case BarcodeFormat.DATA_MATRIX:
                                        string? symbolShape = serviceData.Commands[key].Values[i]["DatamatrixSymbolShape"].StringValue;

                                        options = new DatamatrixEncodingOptions()
                                        {
                                            CharacterSet = characterSetValue,//"UTF-8"
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
                                            CharacterSet = characterSetValue,//"UTF-8"
                                            Width = (int)widthValue,
                                            Height = (int)heightValue,
                                            PureBarcode = pureBarcode ?? false,
                                        };
                                        break;
                                }

                                BarcodeWriter writer = new()
                                {
                                    Format = barcodeFormatSetValue.EnumParse<BarcodeFormat>(),//QR_CODE
                                    Options = options
                                };

                                Bitmap qrCodeBitmap = writer.Write(textValue);


                                if (noSpace == true)
                                {
                                    Color c = qrCodeBitmap.GetPixel(0, 0);
                                    int xResult = 0;
                                    int yResult = 0;
                                    start = new(0, 0);
                                    end = new(qrCodeBitmap.Width - 1, qrCodeBitmap.Height - 1);

                                    bool isOut = false;
                                    for (int x = 0; x < qrCodeBitmap.Height; x++)
                                    {
                                        for (int y = 0; y < qrCodeBitmap.Width; y++)
                                        {
                                            sb.AppendLine($"{x}{y}");
                                            if (c != qrCodeBitmap.GetPixel(x, y))
                                            {
                                                sb.AppendLine($"GG");
                                                //start = new Point(x, y);
                                                xResult = x;
                                                isOut = true;
                                                break;
                                            }
                                        }
                                        if (isOut) break;
                                    }
                                    sb.AppendLine($"");
                                    isOut = false;
                                    for (int y = 0; y < qrCodeBitmap.Width; y++)
                                    {
                                        for (int x = 0; x < qrCodeBitmap.Height; x++)
                                        {
                                            sb.AppendLine($"{x}{y}");
                                            if (c != qrCodeBitmap.GetPixel(x, y))
                                            {
                                                sb.AppendLine($"GG");
                                                //start = new Point(x, y);
                                                yResult = y;
                                                isOut = true;
                                                break;
                                            }
                                        }
                                        if (isOut) break;
                                    }
                                    start = new Point(xResult, yResult);

                                    sb.AppendLine($"");
                                    sb.AppendLine($"");
                                    isOut = false;
                                    for (int x = qrCodeBitmap.Height - 1; x >= 0; x--)
                                    {
                                        for (int y = qrCodeBitmap.Width - 1; y >= 0; y--)
                                        {
                                            sb.AppendLine($"{x}{y}");
                                            if (c != qrCodeBitmap.GetPixel(x, y))
                                            {
                                                sb.AppendLine($"GG");
                                                //end = new Point(x, y);
                                                xResult = x;
                                                isOut = true;
                                                break;
                                            }
                                        }
                                        if (isOut) break;
                                    }
                                    isOut = false;
                                    for (int y = qrCodeBitmap.Width - 1; y >= 0; y--)
                                    {
                                        for (int x = qrCodeBitmap.Height - 1; x >= 0; x--)
                                        {
                                            sb.AppendLine($"{x}{y}");
                                            if (c != qrCodeBitmap.GetPixel(x, y))
                                            {
                                                sb.AppendLine($"GG");
                                                //end = new Point(x, y);
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
            catch (MetaFrmException exception)
            {
                DiagnosticsTool.MyTrace(exception);
                return new Response(exception);
            }
            catch (Exception exception)
            {
                DiagnosticsTool.MyTrace(exception);
                //return new Response(exception);

                return new Response { Status = Status.Failed, Message = $"{exception} {sb}" };
            }


            return response;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:플랫폼 호환성 유효성 검사", Justification = "<보류 중>")]
        private static List<Bitmap> BitmapSeperate(Bitmap bitmapOrg, int seperateCount)
        {
            List<Bitmap> bitmaps = new();
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
    }
}