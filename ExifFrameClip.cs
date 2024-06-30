using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;
using SanyoLib;
/*
 * version 0.0.1 sanyo[JP]
 */
public class ExifFrameClip {
  private static Dictionary<string, Dictionary<string,string>> ReplaceMap;
  private static Color FrameColor;
  private static Color OverlayColor;
  private static double FrameRatio;
  private static List<JsonUtil> labels;
  private static bool CopyMode;
  private static int QualityParamJPEG;
  private static Dictionary<string, StringAlignment> StringAlignments = new Dictionary<string, StringAlignment>(){
    {"LEFT", StringAlignment.Near},
    {"CENTER", StringAlignment.Center},
    {"RIGHT", StringAlignment.Far}
  };
  private static bool TextOnly;
  
  private static void LoadConfig(){
    Match m = Regex.Match(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location),"[\\[\\(]([^\\]\\)]+)[\\)\\]]", RegexOptions.IgnoreCase);
    string configFileName = (m.Success ? m.Groups[1].ToString() : "default.json");
    string configFile = Path.Combine(Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName, Path.Combine("config",configFileName));
    JsonUtil config = File.Exists(configFile) ? JsonUtil.FromString(Regex.Replace(File.ReadAllText(configFile),"\r\n[ \t]*//.*","\r\n", RegexOptions.Multiline)) : JsonUtil.FromString("{}");
    ReplaceMap = config["Replace"] != null ? config["Replace"].GetDictionary().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetDictionary().ToDictionary(kvp2 => kvp2.Key, kvp2 => kvp2.Value.GetString())) : new Dictionary<string, Dictionary<string,string>>();
    FrameColor = ColorTranslator.FromHtml(config["FrameColor"]?.GetString() ?? "#FFFFFF");
    OverlayColor = config["OverlayColor"]==null?FrameColor:ColorTranslator.FromHtml(config["OverlayColor"]?.GetString());
    CopyMode = config["CopyMode"]?.GetBool() ?? false;
    FrameRatio = config["FrameRatio"]?.GetDouble() ?? 0.025;
    labels = config["Label"].GetList();
    QualityParamJPEG = config["CopyModeJpegQuality"]?.GetInt() ?? 90;
    TextOnly = config["TextOnly"]?.GetBool() ?? false;
  }

  [STAThread]
  public static void Main(string[] args) {
    LoadConfig();

    if(args.Length <= 0)
      return;
    string path = args[0];

    string ExifJson = GetExifTool(string.Format("-s -j \"{0}\"", path));
    Console.WriteLine(ExifJson);
    JsonUtil json = JsonUtil.FromString(ExifJson).GetList()[0];
    
    if(TextOnly){
      var labelArea = DrawLabels(null ,json, 0, 0);
      DataObject data = new DataObject(DataFormats.Text, labelArea.Text);
      Clipboard.SetDataObject(data, true);
      return;
    }
    
    using(Bitmap origin = new Bitmap(path)){
      RotateFlipType rotation = new Dictionary<int, RotateFlipType>(){
        {1, RotateFlipType.RotateNoneFlipNone},
        {3, RotateFlipType.Rotate180FlipNone},
        {6, RotateFlipType.Rotate90FlipNone},
        {8, RotateFlipType.Rotate270FlipNone},
      }[Array.Find(origin.PropertyItems, i => (i.Id == 0x0112))?.Value[0]??1];
      origin.RotateFlip(rotation);

      int FrameSize = (int)(FrameRatio > 0 ? origin.Width*FrameRatio : 0);
      int FrameSizeLabel = (int)Math.Abs(origin.Width*FrameRatio);
      int FrameEndHeight = (int)(origin.Height + FrameSizeLabel*(FrameRatio > 0 ? 2 : 1));
      
      StringFormat stringFormat = new StringFormat();
      stringFormat.Alignment = StringAlignment.Center;
      stringFormat.LineAlignment = StringAlignment.Center;

      var labelArea = DrawLabels(null ,json, (int)(FrameSizeLabel==0?origin.Width*0.025:FrameSizeLabel), 0);

      using(Bitmap bitmap = new Bitmap(origin.Width + (FrameSize*2), FrameEndHeight + (int)(Math.Max(labelArea.Top, 0) + FrameSizeLabel)))
      using(Graphics graphics = Graphics.FromImage(bitmap)){
        graphics.FillRectangle(new SolidBrush(FrameColor), 0, 0, bitmap.Width, bitmap.Height);
        graphics.DrawImage(origin, FrameSize, FrameSize, origin.Width, origin.Height);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if(labelArea.Under < 0)
          graphics.FillRectangle(new SolidBrush(OverlayColor), 0, FrameEndHeight+(int)labelArea.Under, bitmap.Width, bitmap.Height);
        
        double offset = FrameEndHeight;
        DrawLabels(graphics ,json, (int)(FrameSizeLabel==0?graphics.VisibleClipBounds.Width*0.025:FrameSizeLabel), offset);

        DataObject data = new DataObject(DataFormats.Bitmap, bitmap);
        data.SetData(DataFormats.Text, labelArea.Text);
        if(CopyMode){
          string tempbase = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location));
          if(!Directory.Exists(tempbase))
            Directory.CreateDirectory(tempbase);
          string tempFile = Path.Combine(tempbase, Path.GetFileNameWithoutExtension(path)+".jpg");//Jpeg
          data.SetData(DataFormats.FileDrop, new string[] { tempFile });
          
          ImageCodecInfo JpegEncoder = Array.Find(ImageCodecInfo.GetImageEncoders(), i => i.FormatID == ImageFormat.Jpeg.Guid);
          EncoderParameters JpegEncoderParm = new EncoderParameters(1);
          JpegEncoderParm.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, QualityParamJPEG);
          bitmap.Save(tempFile, JpegEncoder, JpegEncoderParm);
          //bitmap.Save(tempFile, ImageFormat.Jpeg);//Png  Jpeg
        }
        Clipboard.SetDataObject(data, true);
      }
    }
  }
  private static (double Under, double Top, string Text) DrawLabels(Graphics graphics, JsonUtil exif, double FrameSize, double offset){
    StringBuilder builder = new StringBuilder();
    double underOffset = double.MaxValue;
    using(Bitmap dummy = new Bitmap(1,1))
    using(Graphics dummyG = Graphics.FromImage(dummy)){
      foreach(JsonUtil label in labels){
        string MatchTest = label["MatchTest"]?.GetString();
        if(MatchTest != null && !Regex.Split(MatchTest, "(?<!\\\\\\\\)\\&\\&").Select(j => j.Replace("\\\\&","&")).All( i => {
          string[] m = Regex.Match(i, "(.*)([=!]=)(.*)").Groups.Cast<Group>().Select(j => FormatExifText(j.ToString(), exif, ReplaceMap)).ToArray();
          return (m.Length==4)&&((m[2]=="=="&&m[1]==m[3])||(m[2]=="!="&&m[1]!=m[3]));
        })) continue;
        string text = FormatExifText(label["Format"].GetString(), exif, ReplaceMap);
        if(builder.Length > 0) builder.AppendLine();
        builder.Append(text);
        if(FrameSize!=0){
          StringFormat stringFormat = new StringFormat();
          stringFormat.LineAlignment = StringAlignment.Center;
          stringFormat.Alignment = StringAlignments[(label["Alignment"]?.GetString())??"CENTER"];
          Font font = new Font(label["FontName"].GetString(), (int)(FrameSize * label["FontSize"].GetDouble()), label["FontBold"].GetBool() ? FontStyle.Bold : FontStyle.Regular);
          SizeF drawSize = dummyG.MeasureString(text, font, 10000, stringFormat);
          if(graphics != null)
            using(SolidBrush brush = new SolidBrush(ColorTranslator.FromHtml(label["FontColor"].GetString() ?? "#000000")))
              graphics.DrawString(text, font, brush, new Rectangle((int)FrameSize, (int)offset, (int)(graphics.VisibleClipBounds.Width-(FrameSize*2)), (int)(drawSize.Height*1.1)), stringFormat);
          underOffset = Math.Min(underOffset, offset);
          offset += drawSize.Height + (drawSize.Height * label["AfterSpace"].GetDouble());
          underOffset = Math.Min(underOffset, offset);
        }
      }
    }
    return (Under: underOffset, Top: offset, Text: builder.ToString());
  }
  
  
  private static string FormatExifText(string format, JsonUtil exif, Dictionary<string, Dictionary<string,string>> replaceMap){
    return Regex.Replace(format, "<(\\*)?([^>]+)>", i => {
      string _keyword = i.Groups[2].ToString(), _prefix=string.Empty, _suffix=string.Empty;
      string[] _orKeys = Regex.Split(_keyword, "(?<!\\\\\\\\)\\|").Select(j => j.Replace("\\\\|","|")).ToArray();
      foreach(string k in _orKeys){
        string _key = k;
        string[] _kps = Regex.Split(_key, "(?<!\\\\\\\\)\\+").Select(j => j.Replace("\\\\+","+")).ToArray();
        if(_kps.Length == 3){
          _prefix = _kps[0];
          _key = _kps[1];
          _suffix = _kps[2];
        }
        string temp = exif[_key]?.GetString()?.Trim();
        if(temp != null){
          //ReplaceMap
          if(replaceMap != null && "*".Equals(i.Groups[1].ToString()) && replaceMap.ContainsKey(_key))
            foreach(KeyValuePair<string, string> kvp in replaceMap[_key])
              temp = Regex.Replace(temp, kvp.Key, kvp.Value);//Regex.Escape(
          temp = _prefix+temp+_suffix;
          return temp;
        }
      }
      return null;
    });
  }
  
  private static string GetExifTool(string args, string exe="exiftool"){
    using (Process process = new Process())
    {
      process.StartInfo.FileName = exe;
      process.StartInfo.UseShellExecute = false;
      process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
      process.StartInfo.RedirectStandardOutput = true;
      process.StartInfo.RedirectStandardInput = false;
      process.StartInfo.CreateNoWindow = true;
      process.StartInfo.Arguments = args;

      process.Start();
      string results = process.StandardOutput.ReadToEnd();
      process.WaitForExit();
      return results;
    }
  }
}