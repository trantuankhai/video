using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace FirstWindowsFormApp
{
	public partial class Video : Form
	{
		public Video()
		{
			InitializeComponent();
			this.loadCboTpl(this.cboTemplate, 0, null, "Thêm mới");
			this.loadCboTpl(this.cboSrc, 0, null, "Chưa chọn");
		}

		List<string> listSeconds = new List<string>();
		private List<string> listCode = new List<string>();         //list code ffmpeg
		private bool IsHieroglyphs = false;                         // có phải ký tự đặc biệt không?
		private List<string> listImage = new List<string>();        // danh sách ảnh
		private List<string> listContent = new List<string>();      // danh sách nội dung (text)
		private string randomFolderName = "";                       // biến lưu thư mục tạm @@
		private bool ignore = false;                                // biến check nội dung lỗi
		bool useSub = true;                                     // có sử dụng sub? // tí a check lại cái này
		private int tempRnd = 1;                                    // biến tạm
		int distanceMarginSub = 20;                                 // khoảng cách từ lề tới sub
		string fontName = "Tahoma";                                     // tên font
		int borderColor = -16777216;                                      // mã màu viền
		int borderSize = 3;                                         // cỡ viền
		int textColor = -128;                                           // màu chữ
		int fontSize = 38;                                          // kích thước chữ
		int contentAlign = 1;                                       // căn lề nội dung
		int contentPosition = 1;                                    // vị trí nội dung (giữa trái phải)
		int minWordSub = 15;                                            // số từ tuối thiểu cho 1 đoạn text (ít hơn bỏ qua)
		bool useBackground = true;                                          // sử dụng nền mờ (em để ý xem video sẽ có nền mờ sau ảnh trong video)
		bool chiaChinhXac = false;                                          // à,... cái này là có chia theo dấu phẩy hoặc chấm hay không hay lấy theo
/*		private string tpl;
		private string name;
		private string[] listNameTpl;
*/
		private string linkHome, linkCategory, cssLink, cssTitle, cssContent, cssRemove, removeText, ignoreTitle, cssImage;
		// Tạo event Load form (khi form khởi chạy nó chạy hàm này)
		public Bitmap ResizeImage(Image image, int width, int height)
		{
			Rectangle destRect = new Rectangle(0, 0, width, height);
			Bitmap bitmap = new Bitmap(width, height);
			bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				using (ImageAttributes imageAttributes = new ImageAttributes())
				{
					imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, imageAttributes);
				}
			}
			return bitmap;
		}

		// hàm này là để chạy ffmpeg (cmd) tham số đầu vào là code ở trên
		private void CreateVideoCmd(string args)
		{
			Process process = new Process
			{
				StartInfo =
				{
					FileName = "ffyt.exe",
					Arguments = args,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					WorkingDirectory = Application.StartupPath
				}
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
		}


		// hàm đóng 1 chương trình đang chạy theo tên
		private void CloseWindows(string processName)
		{
			Process[] processesByName = Process.GetProcessesByName(processName);
			foreach (Process process in processesByName)
			{
				try
				{
					process.CloseMainWindow();
					process.Kill();
				}
				catch
				{
				}
			}
		}

		// hàm này anh viết dựa trên cái bài viết của toidicodedao nhé
		// nhưng có 1 vấn đề như này
		// nó bình thường chỉ lấy đc text trong 1 attribute nào đó
		//ví dụ src="https://google.com/search" - cái này ok dễ rồi, vì nó kèm cả domain gốc
		// nhưng có trường hợp thì src="/search" - cái này ko kèm domain gốc, vậy nên a mới phải thêm phần link trang chủ
		// 1 đống else if phía dưới toàn là kiểm tra xem ký tự bắt đầu nó là cái méo gì rồi mới fix đc cái link chuẩn
		// ok a nói vậy thôi có gì e xem lại sau nhé
		private List<string> GetLinkFromHtml(HtmlAgilityPack.HtmlDocument document, string css, string url)
		{
			List<string> list = new List<string>();
			foreach (HtmlNode htmlNode in document.DocumentNode.QuerySelectorAll(css).ToList<HtmlNode>())
			{
				try
				{
					string value = htmlNode.Attributes["href"].Value;
					string value2 = url.Replace("https://", "").Replace("http://", "").Replace("www.", "");
					if (value.Contains(value2))
					{
						list.Add(htmlNode.Attributes["href"].Value);
					}
					else if (value.StartsWith("/"))
					{
						list.Add(url + htmlNode.Attributes["href"].Value);
					}
					else
					{
						list.Add(url + "/" + htmlNode.Attributes["href"].Value);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		private string GetTextFromHtml(HtmlAgilityPack.HtmlDocument document, string css, string rCss = null)
		{
			string[] i = css.Split(new char[]
			{
				',',
				'|'
			});
			string html = "";
			foreach (string c in i)
			{
				List<HtmlNode> items = document.DocumentNode.QuerySelectorAll(c).ToList<HtmlNode>();
				foreach (HtmlNode t in items)
				{
					bool flag = t.InnerText.Trim().Length > 1;
					if (flag)
					{
						html = html + t.InnerHtml + "\r\n";
					}
				}
			}
			bool flag2 = rCss != null && !rCss.Equals("null");
			if (flag2)
			{
				string[] r = rCss.Split(new char[]
				{
					',',
					'|'
				});
				for (int j = 0; j < r.Length; j++)
				{
					List<HtmlNode> items2 = document.DocumentNode.QuerySelectorAll(r[j]).ToList<HtmlNode>();
					foreach (HtmlNode t2 in items2)
					{
						html = html.Replace(t2.InnerHtml, "");
					}
				}
			}
			return CollapseText(html);
		}

		private string CollapseText(string text)
		{
			Regex regex = new Regex("(\\<script(.+?)\\</script\\>)|(\\<style(.+?)\\</style\\>)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			text = text.Replace("<strong>", "").Replace("</strong>", "").Replace("<b>", "").Replace("</b>", "").Replace("<i>", "").Replace("</i>", "");
			text = Regex.Replace(text, "<strong.*?>", "\r\n");
			text = Regex.Replace(text, "<b.*?>", "\r\n");
			text = Regex.Replace(text, "<i.*?>", "\r\n");
			text = regex.Replace(text, "");
			text = HttpUtility.HtmlDecode(text.Trim());
			text = Regex.Replace(text, "<br.*?>", "\r\n");
			text = text.Replace("<br>", "\r\n");
			text = Regex.Replace(text, "<.*?>", "\r\n");
			text = text.Replace("\t", " ");
			text = text.Replace('\t'.ToString(), " ");
			text = Regex.Replace(text, "^\\s+$[\\r\\n]*", "\r\n", RegexOptions.Multiline);
			string[] arr = text.Split(new string[]
			{
				"\r\n"
			}, StringSplitOptions.None);
			text = "";
			foreach (string p in arr)
			{
				bool flag = p.Trim().Length > 3;
				if (flag)
				{
					text = text + p + "\r\n";
				}
			}
			return text.Trim();
		}

		// Token: 0x06000024 RID: 36 RVA: 0x000052F4 File Offset: 0x000034F4
		private string GetOriginalTextFromHtml(HtmlAgilityPack.HtmlDocument document, string css, string rCss = null)
		{
			string[] i = css.Split(new char[]
			{
				',',
				'|'
			});
			string html = "";
			foreach (string c in i)
			{
				List<HtmlNode> items = document.DocumentNode.QuerySelectorAll(c).ToList<HtmlNode>();
				foreach (HtmlNode t in items)
				{
					bool flag = t.InnerText.Trim().Length > 1;
					if (flag)
					{
						html = html + t.InnerHtml + "\r\n";
					}
				}
			}
			bool flag2 = rCss != null && !rCss.Equals("null");
			if (flag2)
			{
				string[] r = rCss.Split(new char[]
				{
					',',
					'|'
				});
				for (int j = 0; j < r.Length; j++)
				{
					List<HtmlNode> items2 = document.DocumentNode.QuerySelectorAll(r[j]).ToList<HtmlNode>();
					foreach (HtmlNode t2 in items2)
					{
						html = html.Replace(t2.InnerHtml, "");
					}
				}
			}
			return html;
		}

		private string FixNumber(string content)
		{
			return content.Replace(".0", "﹒0").Replace(".1", "﹒1").Replace(".2", "﹒2").Replace(".3", "﹒3").Replace(".4", "﹒4").Replace(".5", "﹒5").Replace(".6", "﹒6").Replace(".7", "﹒7").Replace(".8", "﹒8").Replace(".9", "﹒9");
		}

		private string FixFileName(string filename)
		{
			return filename.Replace("/", "").Replace("\\", "").Replace(":", "").Replace("*", "").Replace("?", "").Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", "");
		}

		private double GetAudioFileDuration(string fileName)
		{
			AudioFileReader audioFileReader = new AudioFileReader(fileName);
			double time = audioFileReader.TotalTime.TotalSeconds;
			audioFileReader.Close();
			return time;
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00006560 File Offset: 0x00004760
		private string GetRandomFile(string path, string pattern)
		{
			string[] list = Directory.GetFiles(path, pattern);
			Random rnd = new Random();
			return list[rnd.Next(0, list.Length - 1)].Replace("\\", "/");
		}

		// return command create video
		public string randomStyleVideo(string videoNumber, string seconds)
		{
			Random rnd = new Random();
			int irnd;
			string temp;
			do
			{
				bool flag = Convert.ToDouble(seconds) <= 10.0;
				if (flag)
				{
					irnd = rnd.Next(0, 3);
					String code = listCode[irnd];
					String a = randomFolderName;
					String b = Application.StartupPath;
					temp = listCode[irnd].Replace("#num", videoNumber).Replace("#sec", seconds).Replace("#folder", randomFolderName).Replace("#root", Application.StartupPath);
				}
				else
				{
					bool flag2 = Convert.ToDouble(Convert.ToDouble(seconds)) <= 20.0;
					if (flag2)
					{
						irnd = rnd.Next(4, 7);
						temp = listCode[irnd].Replace("#num", videoNumber).Replace("#sec", seconds).Replace("#folder", randomFolderName).Replace("#root", Application.StartupPath);
					}
					else
					{
						irnd = rnd.Next(8, 11);
						temp = listCode[irnd].Replace("#num", videoNumber).Replace("#sec", seconds).Replace("#folder", randomFolderName).Replace("#root", Application.StartupPath);
					}
				}
			}
			while (irnd == tempRnd || irnd == tempRnd - 4 || irnd == tempRnd - 8 || irnd == tempRnd + 4 || irnd == tempRnd + 8);
			tempRnd = irnd;
			return temp;
		}

		private List<string> GetImageFromHtml(HtmlAgilityPack.HtmlDocument document, string css, string url)
		{
			List<string> link = new List<string>();
			List<string> listImg = new List<string>();
			List<HtmlNode> items = document.DocumentNode.QuerySelectorAll(css.Trim() + " img").ToList<HtmlNode>(); // lấy tất cả thẻ img có trong css selector
			foreach (HtmlNode t in items)
			{
				string i = t.Attributes["src"].Value;
				string u = url.Replace("https://", "").Replace("http://", "").Replace("www.", "");
				bool flag = i.Contains(u);
				if (flag)
				{
					link.Add(t.Attributes["src"].Value);
				}
				else
				{
					bool flag2 = i.StartsWith("/");
					if (flag2)
					{
						link.Add(url + t.Attributes["src"].Value);
					}
					else
					{
						bool flag3 = i.StartsWith("http");
						if (flag3)
						{
							link.Add(t.Attributes["src"].Value);
						}
						else
						{
							link.Add(url + "/" + t.Attributes["src"].Value);
						}
					}
				}
			}
			foreach (string j in link)
			{
				try
				{
					string imgFileName = j.Split(new char[]
					{
						'/'
					})[j.Split(new char[]
					{
						'/'
					}).Length - 1];
					bool flag4 = !imgFileName.EndsWith("jpg") && !imgFileName.EndsWith("png") && !imgFileName.EndsWith("jpeg");
					if (!flag4)
					{
						using (WebClient client = new WebClient())
						{
							client.DownloadFile(new Uri(j), "temp/" + randomFolderName + "/image/" + imgFileName);
							client.Dispose();
						}
						listImg.Add("temp/" + randomFolderName + "/image/" + imgFileName);
					}
				}
				catch
				{
					ignore = true;
				}
			}
			return listImg;
		}

		private void DeleteDirectory(string path)
		{
			try
			{
				string[] files = Directory.GetFiles(path);
				string[] dirs = Directory.GetDirectories(path);
				foreach (string file in files)
				{
					File.SetAttributes(file, FileAttributes.Normal);
					File.Delete(file);
				}
				foreach (string dir in dirs)
				{
					DeleteDirectory(dir);
				}
				Directory.Delete(path, false);
			}
			catch
			{
			}
		}

		private void GetVoiceFromGoogle(string videoNumber, string country, string text)
		{
			try
			{
				string defaultName = "voice";
				bool flag = country == "vi";
				if (flag)
				{
					defaultName = "voice_";
				}
				bool flag2 = text.Trim().Length > 200 && text.Trim().Length <= 380;
				if (flag2)
				{
					string text2 = "";
					string text3 = "";
					int i = 0;
					string[] word = text.Split(new char[]
					{
						' '
					});
					do
					{
						text2 = text2 + word[i] + " ";
						i++;
					}
					while ((text2 + " " + word[i]).Trim().Length <= 200);
					for (int j = i; j < word.Length; j++)
					{
						text3 = text3 + " " + word[j];
					}
					using (WebClient client = new WebClient())
					{
						client.DownloadFile(new Uri(string.Concat(new string[]
						{
							"https://translate.google.com/translate_tts?ie=UTF-8&q=",
							HttpUtility.UrlEncode(text2.Trim()),
							"&tl=",
							country,
							"&total=1&idx=0&client=tw-ob"
						})), string.Concat(new string[]
						{
							"temp/",
							randomFolderName,
							"/",
							defaultName,
							videoNumber,
							"a.mp3"
						}));
						client.Dispose();
					}
					using (WebClient client2 = new WebClient())
					{
						client2.DownloadFile(new Uri(string.Concat(new string[]
						{
							"https://translate.google.com/translate_tts?ie=UTF-8&q=",
							HttpUtility.UrlEncode(text3.Trim()),
							"&tl=",
							country,
							"&total=1&idx=0&client=tw-ob"
						})), string.Concat(new string[]
						{
							"temp/",
							randomFolderName,
							"/",
							defaultName,
							videoNumber,
							"b.mp3"
						}));
						client2.Dispose();
					}
					CreateVideoCmd(string.Concat(new string[]
					{
						"-i \"concat:temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						"a.mp3|temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						"b.mp3\" -acodec copy temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						".mp3"
					}));
					DeleteFile(string.Concat(new string[]
					{
						"temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						"a.mp3"
					}));
					DeleteFile(string.Concat(new string[]
					{
						"temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						"b.mp3"
					}));
				}
				else
				{
					bool flag3 = text.Trim().Length <= 200;
					if (flag3)
					{
						using (WebClient client3 = new WebClient())
						{
							client3.DownloadFile(new Uri(string.Concat(new string[]
							{
								"https://translate.google.com/translate_tts?ie=UTF-8&q=",
								HttpUtility.UrlEncode(text.Trim()),
								"&tl=",
								country,
								"&total=1&idx=0&client=tw-ob"
							})), string.Concat(new string[]
							{
								"temp/",
								randomFolderName,
								"/",
								defaultName,
								videoNumber,
								".mp3"
							}));
							client3.Dispose();
						}
					}
					else
					{
						ignore = true;
					}
				}
				bool flag4 = country == "vi";
				if (flag4)
				{
					CreateVideoCmd(string.Concat(new string[]
					{
						"-i temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						".mp3 -filter:a \"atempo=1.15\" -vn temp/",
						randomFolderName,
						"/voice",
						videoNumber,
						".mp3"
					}));
					DeleteFile(string.Concat(new string[]
					{
						"temp/",
						randomFolderName,
						"/",
						defaultName,
						videoNumber,
						".mp3"
					}));
				}
			}
			catch
			{
				ignore = true;
			}
		}

		private void DeleteFile(string path)
		{
			try
			{
				File.Delete(path);
			}
			catch
			{
			}
		}

		public void CreateBgImage(string content)
		{
			int num = 0;
			int num2 = 1;
			foreach (string text in listContent)
			{
				Bitmap bitmap0 = new Bitmap(listImage[num]);
				Bitmap bitmap = CombineImageBitmap(bitmap0, 1280, 720, listImage[num]);
				string text2 = "bg" + CreateImageName(num2);
				string filename = string.Concat(new string[]
				{
					Application.StartupPath,
					"\\temp\\",
					randomFolderName,
					"\\",
					text2,
					".png"
				});
				num++;
				bool flag = num == listImage.Count;
				if (flag)
				{
					num = 0;
				}
				num2++;
				bitmap.Save(filename, ImageFormat.Png);
				bitmap.Dispose();
				bitmap0.Dispose();
				GC.Collect();
				GC.WaitForPendingFinalizers();
			}
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00003798 File Offset: 0x00001998
		public void CreateSubImage(string content)
		{
			listSeconds.Clear();
			int num = 1;
			List<string> sentenceFromArticle = GetSentenceFromArticle(content, minWordSub, 3);
			foreach (string text in sentenceFromArticle)
			{
				listSeconds.Add(Convert.ToString((double)(text.Split(new char[]
				{
					' '
				}).Length + 1) * 0.45 + 2.0).Replace(",", "."));
				Bitmap bitmap = new Bitmap(Application.StartupPath + "/library/image/transparent.png");
				bool flag = useSub;
				if (flag)
				{
					Graphics graphics = Graphics.FromImage(bitmap);
					StringFormat stringFormat = new StringFormat();
					bool flag2 = contentAlign == 0;
					if (flag2)
					{
						stringFormat.Alignment = StringAlignment.Center;
					}
					else
					{
						bool flag3 = contentAlign == 1;
						if (flag3)
						{
							stringFormat.Alignment = StringAlignment.Near;
						}
						else
						{
							stringFormat.Alignment = StringAlignment.Far;
						}
					}
					bool flag4 = contentPosition == 0;
					if (flag4)
					{
						stringFormat.LineAlignment = StringAlignment.Far;
					}
					else
					{
						bool flag5 = contentPosition == 1;
						if (flag5)
						{
							stringFormat.LineAlignment = StringAlignment.Center;
						}
						else
						{
							stringFormat.LineAlignment = StringAlignment.Near;
						}
					}
					stringFormat.LineAlignment = StringAlignment.Far;
					Font font = new Font(fontName, (float)fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
					Pen pen = new Pen(Color.FromArgb(borderColor), (float)borderSize);
					pen.LineJoin = LineJoin.Round;
					Rectangle rect = new Rectangle(0, bitmap.Height - font.Height, bitmap.Width, font.Height);
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(rect, Color.FromArgb(textColor), Color.FromArgb(textColor), 90f);
					Rectangle layoutRect = new Rectangle(distanceMarginSub, distanceMarginSub, bitmap.Width - distanceMarginSub * 2, bitmap.Height - distanceMarginSub * 2);
					GraphicsPath graphicsPath = new GraphicsPath();
					graphicsPath.AddString(text, font.FontFamily, (int)font.Style, (float)fontSize, layoutRect, stringFormat);
					graphics.SmoothingMode = SmoothingMode.AntiAlias;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.DrawPath(pen, graphicsPath);
					graphics.FillPath(linearGradientBrush, graphicsPath);
					graphicsPath.Dispose();
					linearGradientBrush.Dispose();
					linearGradientBrush.Dispose();
					font.Dispose();
					stringFormat.Dispose();
					graphics.Dispose();
				}
				string text2 = "sub" + CreateImageName(num);
				string filename = string.Concat(new string[]
				{
					Application.StartupPath,
					"\\temp\\",
					randomFolderName,
					"\\",
					text2,
					".png"
				});
				num++;
				bitmap.Save(filename, ImageFormat.Png);
				bitmap.Dispose();
				GC.Collect();
				GC.WaitForPendingFinalizers();
			}
		}

		private void getTempleteFromFile()
        {
			string[] tpl = File.ReadAllLines("tpl/" + this.cboSrc.SelectedItem + ".tp");
			this.linkHome = tpl[0];
			this.linkCategory = tpl[1];
			this.cssLink = tpl[2];
			this.cssTitle = tpl[3];
			this.cssContent = tpl[4];
			this.cssRemove = tpl[5];
			this.removeText = tpl[6];
			this.ignoreTitle = tpl[7];
			this.cssImage = tpl[4];
			bool flag = this.cssRemove.Trim().Length == 0;
			if (flag)
			{
				this.cssRemove = null;
			}
			bool flag2 = this.removeText.Trim().Length == 0;
			if (flag2)
			{
				this.removeText = null;
			}
			bool flag3 = this.ignoreTitle.Trim().Length == 0;
			if (flag3)
			{
				this.ignoreTitle = null;
			}
		}
		private void run()
		{
			if (cboSrc.SelectedIndex != 0)
            {
				int soVideo = (int)numVideo.Value;
				String linkBaiViet = "";
				getTempleteFromFile();
				if (linkCategory.Contains("{page}"))
				{
					string[] array = linkCategory.Trim().Split(new char[]{'|'});
					string linkPage = array[0];                 // link chuyên mục {page}			
					int pageTo = Convert.ToInt32(array[1]);    // lấy từ page 1
					int pageFrom = Convert.ToInt32(array[2]);   // tới page 2
					HtmlWeb htmlWeb = new HtmlWeb {AutoDetectEncoding = false,OverrideEncoding = Encoding.UTF8};
					for (int i = pageTo; i <= pageFrom; i++)
					{
						HtmlAgilityPack.HtmlDocument document = htmlWeb.Load(linkPage.Replace("{page}", string.Concat(i)));// lấy dữ liệu từ trang chuyên mục
						List<string> linkFromHtml = GetLinkFromHtml(document, cssLink, linkHome);       // lấy ra danh sách link bài viết từ Css Selector
						if(linkFromHtml == null)
                        {
							MessageBox.Show("Không tìm thấy link trong trang đích");
							return;
                        }
						if (soVideo != 0)
						{
							if(soVideo > linkFromHtml.Count)
                            {
								MessageBox.Show("Vị trí bài viết vượt quá "+ linkFromHtml.Count+" bài");
                            }
                            else
                            {
								linkBaiViet = linkFromHtml[soVideo - 1];
								createVideo(linkBaiViet);
							}
						}
						else
						{
							for (int j = 0; j < linkFromHtml.Count; j++)            // vòng lặp tất cả các bài viết đã get đc từ page 1 và page 2
							{
								createVideo(linkFromHtml[j]);
							}
						}
					}
					return;
                }
                else
                {
					HtmlWeb htmlWeb = new HtmlWeb{AutoDetectEncoding = false,OverrideEncoding = Encoding.UTF8};
					HtmlAgilityPack.HtmlDocument document = htmlWeb.Load(linkCategory);
					List<string> linkFromHtml = GetLinkFromHtml(document, cssLink, linkHome);
					if (linkFromHtml.Count == 0)
					{
						MessageBox.Show("Không tìm thấy link trong trang đích");
						return;
					}
					if (soVideo != 0)
					{
						if (soVideo > linkFromHtml.Count)
						{
							MessageBox.Show("Vị trí bài viết vượt quá " + linkFromHtml.Count + " bài");
						}
						else
						{
							linkBaiViet = linkFromHtml[soVideo - 1];
							createVideo(linkBaiViet);
						}
					}
					else
					{
						for (int j = 0; j < linkFromHtml.Count; j++)            // vòng lặp tất cả các bài viết đã get đc từ page 1 và page 2
						{
							createVideo(linkFromHtml[j]);
						}
					}
				}
			}
            else
            {
				MessageBox.Show("Chưa chọn nguồn", "Thông báo");
            }
		}
		public void createVideo(String linkPost)
		{
			HtmlWeb htmlWeb2 = new HtmlWeb{AutoDetectEncoding = false,OverrideEncoding = Encoding.UTF8};
			try
			{
				HtmlAgilityPack.HtmlDocument document2 = htmlWeb2.Load(linkPost); 
				string textFromHtml = GetTextFromHtml(document2, cssTitle, null);   
				string contentPage = StandardContent(FixNumber(GetTextFromHtml(document2, cssContent, cssRemove))).Replace("(*)", "");
				if (!File.Exists("output/" + FixFileName(textFromHtml) + ".mp4")) //check trùng video
				{
					string[] array2 = contentPage.Replace("\r", "").Split(new char[]{'\n'});
/*					for (int k = 0; k < array2.Length; k++)
					{
						int length = array2[k].Trim().Length;
					}*/
					listContent.Clear();
					listImage.Clear();
					// Guid.NewGuid() là tạo ra 1 mã guid hầu như 99,9% là không trùng nhau, nên đa số họ dùng hàm này để
					// tạo ra 1 text random 
					randomFolderName = Guid.NewGuid().ToString().Replace("-", "");
					Directory.CreateDirectory("temp/" + randomFolderName);              //tạo thư mục
					Directory.CreateDirectory("temp/" + randomFolderName + "/image");
					string text3 = contentPage;
					listImage = GetImageFromHtml(document2, cssImage, cssLink);
					if (ignore)
					{
						ignore = false;
						DeleteDirectory("temp/" + randomFolderName);
					}
					else
					{
						listContent = GetSentenceFromArticle(contentPage, minWordSub, 3);
						//nếu ảnh tải về ít hơn 3 ảnh thì không tạo video nữa, em muốn sửa lại thì thay trức tiếp nhé
						if (listImage.Count < 1)
						{
							DeleteDirectory("temp/" + randomFolderName);
						}
						else
						{
							Thread thread1 = new Thread(() =>{
								// createbgimage là nó tạo video nền từ ảnh đã tải về !!!
								CreateBgImage(contentPage);
								
							});
							Thread thread2 = new Thread(() => {
								// createbgimage là nó tạo video nền từ ảnh đã tải về !!!
								CreateSubImage(contentPage);
							});
							thread1.Start();
							thread2.Start();

							int countImageBgInFolder = Directory.GetFiles("temp/" + randomFolderName, "bg*.png").Length;
							string text6 = "";
							for (int i = 0; i < countImageBgInFolder; i++)
							{
								// phần này là đánh số thứ tự ảnh
								string nameAutoFile;
								if ((i + 1).ToString().Length == 1)
								{
									nameAutoFile = "00" + (i + 1);
								}
								else if ((i + 1).ToString().Length == 2)
								{
									nameAutoFile = "0" + (i + 1);
								}
								else
								{
									nameAutoFile = string.Concat(i + 1);
								}
								// lấy voice từ google đây, tham số thứ 2 là thứ tiếng "vi"
								// cái này lại có ignore, chắc là kiểm tra xem từ có hợp lệ ko :v
								GetVoiceFromGoogle(nameAutoFile, "vi", listContent[i]);
								if (ignore)
								{
									ignore = false;
									DeleteDirectory("temp/" + randomFolderName);
								}
								else
								{
									// thỏa mãn tất cả xong thì nó sẽ tạo video từ ảnh nền đã tải, và ảnh sub
									// cái randomvideo kia chỉ là nó ngẫu nhiên chọn 1 trong 6-8 kiểu video chạy gì đó thôi
									// nó chạy lên xuống, trái sang phải, phải sang trái, zoom in zoom out,....
									// phần code ffmpeg nó là 1 ngôn ngữ khác nên anh ko đụng tới nhé
									// cái getaudiofileduration, hàm này lấy độ dài giọng nói của chị Google, xem chị ấy ngắt lời ở đâ
									// thì tạo autodio tới đó thôi :v

									CreateVideoCmd(randomStyleVideo(nameAutoFile, string.Concat(GetAudioFileDuration(string.Concat(new string[]{"temp/",randomFolderName,"/voice",nameAutoFile,".mp3"})))));

									CreateVideoCmd(string.Concat(new string[]
									{
													"-i temp/",
													randomFolderName,
													"/vid_",
													nameAutoFile,
													".ts -i temp/",
													randomFolderName,
													"/voice",
													nameAutoFile,
													".mp3 -c:v copy -c:a copy -strict experimental temp/",
													randomFolderName,
													"/vid",
													nameAutoFile,
													".ts"
									}));
									if (text6 == "")
									{
										text6 = string.Concat(new string[]
										{
														"concat:temp/",
														randomFolderName,
														"/vid",
														nameAutoFile,
														".ts"
										});
									}
									else
									{
										text6 = string.Concat(new string[]
										{
														text6,
														"|temp/",
														randomFolderName,
														"/vid",
														nameAutoFile,
														".ts"
										});
									}
								}
							}
							CreateVideoCmd(string.Concat(new string[]
							{
											"-i \"",
											text6,
											"\" -c:v copy -c:a copy \"temp/",
											randomFolderName,
											"/final_.mp4\""
							}));
							CreateVideoCmd(string.Concat(new string[]
							{
											"-i \"temp/",
											randomFolderName,
											"/final_.mp4\" -i \"",
											GetRandomFile("library/sound", "*.mp3"),
											"\" -shortest -c:v copy -filter_complex \"[0:a]aformat=fltp:44100:stereo,apad[0a];[1]aformat=fltp:44100:stereo,volume=0.1[1a];[0a][1a]amerge[a]\" -map 0:v -map \"[a]\" -ac 2 \"temp/",
											randomFolderName,
											"/final.mp4\""
							}));
							File.Move("temp/" + randomFolderName + "/final.mp4", (locationSaveFile != null) ? "output/" + FixFileName(textFromHtml.Trim()) + ".mp4" : "locationSaveFile/" + FixFileName(textFromHtml.Trim()) + ".mp4");
							if (text3.Length >= 4850)
							{
								text3 = contentPage.Substring(0, 4850) + "...";
							}
							listImage.Clear();
							//CreateThumbnail("output/" + FixFileName(textFromHtml) + ".png", content);
							DeleteDirectory("temp/" + randomFolderName);
						}
					}
				}
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
				DeleteDirectory("temp/" + randomFolderName);
			}
		}

		// đây là hàm tạo ảnh từ nội dung, tại sao phải tạo ảnh từ nội dung
		// vì ffmpeg không hỗ trợ chèn text vào 1 vị trí cụ thể, nên a phải có hàm này để tạo ra 1 ảnh chứa sẵn text
		// mấy ảnh này có nhiệm vụ làm nền (trong suốt) để hiện thị chữ
		public void CreateImage(string content)
		{
			int num = 0;
			int num2 = 1;
			List<string> sentenceFromArticle = GetSentenceFromArticle(content, minWordSub, 3);
			foreach (string text in sentenceFromArticle)
			{
				Bitmap bitmap = CombineImageBitmap(new Bitmap(listImage[num]), 1280, 720, listImage[num]);
				bool flag = useSub;
				if (flag)
				{
					Graphics graphics = Graphics.FromImage(bitmap);
					StringFormat stringFormat = new StringFormat();
					bool flag2 = contentAlign == 0;
					if (flag2)
					{
						stringFormat.Alignment = StringAlignment.Center;
					}
					else
					{
						bool flag3 = contentAlign == 1;
						if (flag3)
						{
							stringFormat.Alignment = StringAlignment.Near;
						}
						else
						{
							stringFormat.Alignment = StringAlignment.Far;
						}
					}
					bool flag4 = contentPosition == 0;
					if (flag4)
					{
						stringFormat.LineAlignment = StringAlignment.Far;
					}
					else
					{
						bool flag5 = contentPosition == 1;
						if (flag5)
						{
							stringFormat.LineAlignment = StringAlignment.Center;
						}
						else
						{
							stringFormat.LineAlignment = StringAlignment.Near;
						}
					}
					stringFormat.LineAlignment = StringAlignment.Far;
					Font font = new Font(fontName, (float)fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
					Pen pen = new Pen(Color.FromArgb(borderColor), (float)borderSize);
					pen.LineJoin = LineJoin.Round;
					Rectangle rect = new Rectangle(0, bitmap.Height - font.Height, bitmap.Width, font.Height);
					LinearGradientBrush linearGradientBrush = new LinearGradientBrush(rect, Color.FromArgb(textColor), Color.FromArgb(textColor), 90f);
					Rectangle layoutRect = new Rectangle(distanceMarginSub, distanceMarginSub, bitmap.Width - distanceMarginSub * 2, bitmap.Height - distanceMarginSub * 2);
					GraphicsPath graphicsPath = new GraphicsPath();
					graphicsPath.AddString(text, font.FontFamily, (int)font.Style, (float)fontSize, layoutRect, stringFormat);
					graphics.SmoothingMode = SmoothingMode.AntiAlias;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.DrawPath(pen, graphicsPath);
					graphics.FillPath(linearGradientBrush, graphicsPath);
					graphicsPath.Dispose();
					linearGradientBrush.Dispose();
					linearGradientBrush.Dispose();
					font.Dispose();
					stringFormat.Dispose();
					graphics.Dispose();
				}
				string text2 = "img" + CreateImageName(num2);
				string filename = string.Concat(new string[]
				{
					Application.StartupPath,
					"\\temp\\",
					randomFolderName,
					"\\",
					text2,
					".png"
				});
				num++;
				bool flag6 = num == listImage.Count;
				if (flag6)
				{
					num = 0;
				}
				num2++;
				bitmap.Save(filename, ImageFormat.Png);
				bitmap.Dispose();
				GC.Collect();
				GC.WaitForPendingFinalizers();
			}
		}

		public string CreateImageName(int t)
		{
			bool flag = t.ToString().Length == 1;
			string result;
			if (flag)
			{
				result = "00" + t;
			}
			else
			{
				bool flag2 = t.ToString().Length == 2;
				if (flag2)
				{
					result = "0" + t;
				}
				else
				{
					result = t.ToString();
				}
			}
			return result;
		}

		public Bitmap CombineImageBitmap(Image image, int width, int height, string fileBg)
		{
			bool flag = !useBackground;
			Bitmap result;
			if (flag)
			{
				result = ResizeImage(image, width, height);
			}
			else
			{
				bool flag2 = image.Width > width && image.Height > height;
				if (flag2)
				{
					bool flag3 = image.Height < image.Width;
					int num;
					int num2;
					if (flag3)
					{
						num = width;
						num2 = width * image.Height / image.Width;
					}
					else
					{
						num = height * image.Width / image.Height;
						num2 = height;
					}
					image = ResizeImage(image, num, num2);
				}
				else
				{
					bool flag4 = image.Width < width;
					if (flag4)
					{
						bool flag5 = image.Height < image.Width;
						int num3;
						int num4;
						if (flag5)
						{
							num3 = ((image.Width < 900) ? 900 : image.Width);
							num4 = num3 * image.Height / image.Width;
						}
						else
						{
							num3 = height * image.Width / image.Height;
							num4 = height;
						}
						image = ResizeImage(image, num3, num4);
					}
					else
					{
						bool flag6 = image.Height < height;
						if (flag6)
						{
							bool flag7 = image.Width < image.Height;
							int num5;
							int num6;
							if (flag7)
							{
								num5 = ((image.Height < 600) ? 600 : image.Height);
								num6 = num5 * image.Width / image.Height;
							}
							else
							{
								num5 = width * image.Height / image.Width;
								num6 = width;
							}
							image = ResizeImage(image, num6, num5);
						}
					}
				}
				using (Graphics graphics = Graphics.FromImage(image))
				{
					graphics.DrawRectangle(new Pen(Brushes.White, 20f), new Rectangle(0, 0, image.Width, image.Height));
				}
				Image image2 = ResizeImage(Image.FromFile(fileBg), 1280, 720);
				Bitmap bitmap = new Bitmap(width, height);
				using (Graphics graphics2 = Graphics.FromImage(bitmap))
				{
					graphics2.DrawImage(image2, new Rectangle(0, 0, 1280, 720));
					graphics2.DrawImage(new Bitmap(Application.StartupPath + "/library/image/blurbg.png"), new Rectangle(0, 0, 1280, 720));
				}
				Bitmap bitmap2 = new Bitmap(width, height);
				using (Graphics graphics3 = Graphics.FromImage(bitmap2))
				{
					graphics3.CompositingMode = CompositingMode.SourceCopy;
					graphics3.CompositingQuality = CompositingQuality.HighQuality;
					graphics3.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics3.SmoothingMode = SmoothingMode.HighQuality;
					graphics3.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics3.DrawImageUnscaled(bitmap, 0, 0);
					graphics3.DrawImageUnscaledAndClipped(image, new Rectangle((bitmap2.Width - image.Width) / 2, (bitmap2.Height - image.Height) / 2, image.Width, image.Height));
				}
				image2.Dispose();
				bitmap.Dispose();
				result = bitmap2;
			}
			return result;
		}
		// Token: 0x06000003 RID: 3 RVA: 0x00002618 File Offset: 0x00000818
		private void loadCboTpl(ComboBox cbo, int index = 0, string item = null, string firstItem = null)
		{
			cbo.Items.Clear();
			bool flag = firstItem != null;
			if (flag)
			{
				cbo.Items.Add(firstItem);
			}
			string[] listTpl = Directory.GetFiles("tpl", "*.tp");
			bool flag2 = listTpl.Length != 0;
			if (flag2)
			{
				foreach (string tpl in listTpl)
				{
					cbo.Items.Add(tpl.Replace(".tp", "").Replace("tpl/", "").Replace("tpl\\", ""));
				}
				bool flag3 = item == null;
				if (flag3)
				{
					cbo.SelectedIndex = index;
				}
				else
				{
					cbo.SelectedItem = item;
				}
			}
		}
		public List<string> GetSentenceFromArticle(string content, int length, int ignoreLeng)
		{
			List<string> list = new List<string>();
			bool flag3 = chiaChinhXac;
			List<string> result;
			if (flag3)
			{
				bool isHieroglyphs = IsHieroglyphs;
				if (isHieroglyphs)
				{
					string text2 = "";
					int num = 0;
					for (int i = 0; i < content.Length; i++)
					{
						text2 += content[i].ToString();
						num++;
						bool flag4 = num == length;
						if (flag4)
						{
							list.Add(text2);
							text2 = "";
							num = 0;
						}
						else
						{
							bool flag5 = i == content.Length - 1;
							if (flag5)
							{
								list.Add(text2);
								break;
							}
						}
					}
				}
				else
				{
					string[] array3 = content.Split(new char[]
					{
						' '
					});
					string text3 = "";
					int num2 = 0;
					for (int j = 0; j < array3.Length; j++)
					{
						text3 = text3 + array3[j] + " ";
						num2++;
						bool flag6 = num2 == length;
						if (flag6)
						{
							list.Add(text3);
							text3 = "";
							num2 = 0;
						}
						else
						{
							bool flag7 = j == array3.Length - 1;
							if (flag7)
							{
								list.Add(text3);
								break;
							}
						}
					}
				}
				result = list;
			}
			else
			{
				string[] array4 = content.Split(new string[]
				{
					". ",
					"\r\n",
					Environment.NewLine
				}, StringSplitOptions.None);
				string text4 = "";
				bool isHieroglyphs2 = IsHieroglyphs;
				if (isHieroglyphs2)
				{
					bool flag8 = array4.Length == 1;
					if (flag8)
					{
						array4 = content.Split(new string[]
						{
							"。"
						}, StringSplitOptions.None);
					}
					int num3 = 0;
					foreach (string text5 in array4)
					{
						num3++;
						bool flag9 = !(text5.Trim() == "");
						if (flag9)
						{
							bool flag10 = text5.Length < ignoreLeng && text4 != "";
							if (flag10)
							{
								list.Add(text4);
								text4 = "";
							}
							else
							{
								bool flag11 = (text4 + text5).Length < length;
								if (flag11)
								{
									text4 = text4 + text5 + ". ";
									bool flag12 = num3 == array4.Length;
									if (flag12)
									{
										list.Add(text4);
									}
								}
								else
								{
									bool flag = false;
									bool flag13 = text4 == "";
									if (flag13)
									{
										text4 = text5 + ".";
										flag = true;
									}
									else
									{
										bool flag14 = text4.Length < ignoreLeng;
										if (flag14)
										{
											text4 += (text4.Contains(".") ? (text5 + ".") : (". " + text5 + "."));
											flag = true;
										}
										else
										{
											bool flag15 = num3 == array4.Length;
											if (flag15)
											{
												text4 = text4 + ". " + text5;
												flag = true;
											}
										}
									}
									bool flag16 = text4.Length > length * 2;
									if (flag16)
									{
										int num4 = 0;
										string text6 = "";
										for (int k = 0; k < text4.Length; k++)
										{
											num4++;
											text6 += text4[k].ToString();
											bool flag17 = num4 == text4.Length / 2 + 1;
											if (flag17)
											{
												num4 = 0;
												list.Add(text6);
												text6 = "";
											}
											else
											{
												bool flag18 = k == text4.Length - 1;
												if (flag18)
												{
													list.Add(text6);
												}
											}
										}
									}
									else
									{
										list.Add(text4);
										bool flag19 = !flag;
										if (flag19)
										{
											list.Add(text5);
										}
									}
									text4 = "";
								}
							}
						}
					}
				}
				else
				{
					bool flag20 = !content.Contains('.');
					if (flag20)
					{
						string[] array5 = content.Split(new char[]
						{
							' '
						});
						int num5 = 0;
						string text7 = "";
						foreach (string str in array5)
						{
							num5++;
							text7 = text7 + str + " ";
							bool flag21 = num5 == length;
							if (flag21)
							{
								list.Add(text7);
								num5 = 0;
								text7 = "";
							}
						}
						return list;
					}
					int num6 = 0;
					foreach (string text8 in array4)
					{
						num6++;
						bool flag22 = !(text8.Trim() == "");
						if (flag22)
						{
							bool flag23 = text8.Length < ignoreLeng && text4 != "";
							if (flag23)
							{
								list.Add(text4);
								text4 = text8;
							}
							else
							{
								bool flag24 = (text4 + text8).Split(new char[]
								{
									' '
								}).Length < length;
								if (flag24)
								{
									bool flag25 = !content.Contains(text4 + text8) || !content.Contains(text4.Trim() + " " + text8.Trim()) || !content.Contains(text4.Trim() + text8.Trim()) || !content.Contains(text4.Trim() + ". " + text8.Trim()) || !content.Contains(text4.Trim() + "." + text8.Trim());
									if (flag25)
									{
										text4 = text4 + Environment.NewLine + text8 + ". ";
									}
									else
									{
										text4 = text4 + text8 + ". ";
									}
									bool flag26 = num6 == array4.Length;
									if (flag26)
									{
										list.Add(text4);
									}
								}
								else
								{
									bool flag2 = false;
									bool flag27 = text4 == "";
									if (flag27)
									{
										text4 = text8 + ".";
										flag2 = true;
									}
									else
									{
										bool flag28 = text4.Length < ignoreLeng;
										if (flag28)
										{
											text4 += (text4.Contains(".") ? (text8 + ".") : (". " + text8 + "."));
											flag2 = true;
										}
										else
										{
											bool flag29 = num6 == array4.Length;
											if (flag29)
											{
												text4 = text4 + ". " + text8;
												flag2 = true;
											}
										}
									}
									bool flag30 = text4.Split(new char[]
									{
										' '
									}).Length > length * 2;
									if (flag30)
									{
										string[] tempArr = text4.Split(new char[]
										{
											','
										});
										bool flag31 = tempArr.Length >= 2;
										if (flag31)
										{
											string[] array6 = tempArr;
											string text9 = "";
											for (int num7 = 0; num7 < array6.Length; num7++)
											{
												string temp = text9;
												bool flag32 = text9 != "";
												if (flag32)
												{
													text9 += ",";
												}
												text9 += array6[num7];
												bool flag33 = text9.Trim().Length > 200;
												if (flag33)
												{
													bool flag34 = num7 != array6[num7].Length - 1;
													if (flag34)
													{
														list.Add(temp + "…");
													}
													else
													{
														list.Add(temp);
													}
													bool flag35 = num7 == array6.Length - 1;
													if (flag35)
													{
														list.Add(array6[num7]);
													}
													else
													{
														text9 = "";
													}
												}
												else
												{
													bool flag36 = num7 == array6.Length - 1;
													if (flag36)
													{
														list.Add(text9);
													}
												}
											}
										}
										else
										{
											string[] array7 = text4.Split(new char[]
											{
												' '
											});
											int num8 = 0;
											string text10 = "";
											for (int num9 = 0; num9 < array7.Length; num9++)
											{
												num8++;
												text10 = text10 + array7[num9] + " ";
												bool flag37 = num8 == array7.Length / 2 + 1;
												if (flag37)
												{
													num8 = 0;
													list.Add(text10);
													text10 = "";
												}
												else
												{
													bool flag38 = num9 == array7.Length - 1;
													if (flag38)
													{
														list.Add(text10);
													}
												}
											}
										}
									}
									else
									{
										list.Add(text4);
										bool flag39 = !flag2;
										if (flag39)
										{
											list.Add(text8 + ".");
										}
									}
									text4 = "";
								}
							}
						}
					}
				}
				result = list;
			}
			return FixContentArray(result);
		}

		// mấy hàm này để fix nội dung chữ ví dụ như có 2 dấu cách liên tục thì thay thế bằng 1 cái,...
		// bình thường thì cái này có thể dùng regex nhưng 2 năm trước a code như này cho nó nhanh :v
		private string StandardContent(string content)
		{
			return content.Replace("   ", " ").Replace("  ", " ").Replace("...", "…").Replace("..", "…").Replace("-", "- ").Replace(".", ". ").Replace("!", "! ").Replace("?", "? ").Replace(" ?", "?").Replace(" !", "!").Replace(" .", ".").Replace("   ", " ").Replace("  ", " ");
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00006428 File Offset: 0x00004628
		private List<string> FixContentArray(List<string> result)
		{
			for (int i = 0; i < result.Count; i++)
			{
				result[i] = result[i].Replace("﹒", ".").Replace("   ", " ").Replace("  ", " ").Replace("..", ".").Replace(". .", ".").Replace(":.", ":").Replace("?.", "?").Replace("!.", "!").Replace(": .", ":").Replace("? .", "?").Replace("! .", "!").Replace("   ", " ").Replace("  ", " ");
			}
			return result;
		}

		public void status(string value)
		{
			bool invokeRequired = base.InvokeRequired;
			if (invokeRequired)
			{
				base.Invoke(new Action<string>(this.status), new object[]
				{
					value
				});
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			run();
		}

		private void button2_Click(object sender, EventArgs e)
		{

		}
		// nếu chọn mẫu cũ thì tiến hành cập nhập
		// còn nếu chọn thêm mới thì sẽ thêm một file mới
		private void DeleteFile_(string path)
		{
			try
			{
				File.Delete(path);
			}
			catch
			{
			}
		}

        private void Video_Load(object sender, EventArgs e)
        {

        }

        private void btnDelete_Click_1(object sender, EventArgs e)
        {
			bool flag = this.cboTemplate.SelectedIndex != 0;
			if (flag)
			{
				if (MessageBox.Show("Ban có muốn xóa", "Message", MessageBoxButtons.YesNo) == DialogResult.Yes)
				{
					File.Delete("tpl/" + this.cboTemplate.SelectedItem + ".tp");
					this.loadCboTpl(this.cboTemplate, 0, null, "Thêm mới");
					this.loadCboTpl(this.cboSrc, 0, null, "Chưa chọn");
				}
			}
			else
			{
				MessageBox.Show("Chọn templete cần xóa", "Messenge", MessageBoxButtons.OK);
			}
		}

        private void btnSave_Click(object sender, EventArgs e)
        {
			string name = "";
			string tpl = string.Concat(new string[]
			{
				this.txtLinkHome.Text,
				"\r\n",
				this.txtLinkCategory.Text,
				"\r\n",
				this.txtCSSLink.Text,
				"\r\n",
				this.txtCSSTitle.Text,
				"\r\n",
				this.txtCSSContent.Text,
				"\r\n",
				this.txtCSSRemove.Text,
				"\r\n",
				this.txtTextRemove.Text,
				"\r\n",
				this.txtIgnoreTitle.Text,
				"\r\n"
			});
			bool flag = this.cboTemplate.SelectedIndex != 0;
			// cập nhập
			if (flag == true)
			{
				// get current name
				name = (string.Concat(this.cboTemplate.SelectedItem) ?? "");
				// get list name
				File.Delete("tpl/" + name + ".tp");
				File.WriteAllText("tpl/" + name + ".tp", tpl);
				MessageBox.Show("Cập nhập mẫu thành công");
			}
			// thêm mới
			else
			{
				string[] listTpl = Directory.GetFiles("tpl", "*.tp");
				bool listTplString = listTpl.Contains(name);
				name = this.txtName.Text;
				if (name.Length == 0)
				{
					MessageBox.Show("Tên không được rỗng");
					this.txtName.Focus();
				}
				else if (this.txtLinkHome.Text.Length == 0)
				{
					MessageBox.Show("Link trang chủ không được rỗng");
					this.txtLinkHome.Focus();
				}
				else if (this.txtLinkCategory.Text.Length == 0)
				{
					MessageBox.Show("Link danh mục không được rỗng");
					this.txtLinkCategory.Focus();
				}
				else if (this.txtCSSLink.Text.Length == 0)
				{
					MessageBox.Show("Selector link bài viết không được rỗng");
					this.txtCSSLink.Focus();
				}
				else if (this.txtCSSTitle.Text.Length == 0)
				{
					MessageBox.Show("Selector tiêu đề bài viết không được rỗng");
					this.txtCSSTitle.Focus();
				}
				else if (this.txtCSSContent.Text.Length == 0)
				{
					MessageBox.Show("Selector nội dung  bài viết không được rỗng");
					this.txtCSSContent.Focus();
				}
				else if (listTplString)
				{
					MessageBox.Show("Tên đã tồn tại");
					this.txtName.Focus();
				}
				else
				{
					File.WriteAllText("tpl/" + this.txtName.Text + ".tp", tpl);
					MessageBox.Show("Thêm mẫu thành công");
					this.loadCboTpl(this.cboTemplate, 0, null, "Thêm mới");
					this.loadCboTpl(this.cboSrc, 0, null, "Chưa chọn");

				}
			}
		}

        private void btnStart_Click(object sender, EventArgs e)
        {
			//this.btnStart.Enabled = false;
			run();
        }
		private string locationSaveFile = null;
        private void button1_Click_1(object sender, EventArgs e)
        {
			FolderBrowserDialog fd = new FolderBrowserDialog();
			DialogResult result = fd.ShowDialog();
			//SaveFileDialog saveFile = new SaveFileDialog();
			if(result == DialogResult.OK)
            {
				locationSaveFile = fd.SelectedPath;

			}
        }

        private void label14_Click(object sender, EventArgs e)
        {
			
        }

        private void Video_Load_1(object sender, EventArgs e)
        {
			loadCmdRenderVideo();
			loadSettingVideo();
		}
		private void loadCmdRenderVideo()
        {
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=min(max(zoom\\,pzoom)+0.0020\\,1.5):d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg]; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=if(lte(zoom\\,1.0)\\,1/0.5\\,max(1.0\\,zoom+-0.0020)):d=25*11:x='iw/2-(iw/zoom/2)': y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.25:h=3*720/1.3:x=t*(in_w-out_w)/11, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.25:h=3*720/1.3:x=(in_w-out_w)-t*(in_w-out_w)/11, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=min(max(zoom\\,pzoom)+0.0012\\,1.5):d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg]; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=if(lte(zoom\\,1.0)\\,1/0.5\\,max(1.0\\,zoom+-0.0016)):d=25*21:x='iw/2-(iw/zoom/2)': y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.3:h=3*720/1.35:x=t*(in_w-out_w)/21, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.3:h=3*720/1.35:x=(in_w-out_w)-t*(in_w-out_w)/21, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=min(max(zoom\\,pzoom)+0.0012\\,1.6):d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg]; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280:h=3*720, zoompan=z=if(lte(zoom\\,1.0)\\,1/0.4\\,max(1.0\\,zoom+-0.0016)):d=25*31:x='iw/2-(iw/zoom/2)': y='ih/2-(ih/zoom/2)':s=1280x720, setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.35:h=3*720/1.4:x=t*(in_w-out_w)/31, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
			listCode.Add("-y -loop 1 -i \"#root/temp/#folder/bg#num.png\" -ss 0 -t #sec -r 30 -loop 1 -i \"#root/temp/#folder/sub#num.png\" -ss 0 -t #sec -filter_complex \" [1:v] scale=w=1280:h=720 [fg]; [0:v] scale=w=-2:h=3*720 , crop=w=3*1280/1.4:h=3*720/1.35:x=(in_w-out_w)-t*(in_w-out_w)/31, scale=w=1280:h=720,  setsar=1 [bg] ; [bg][fg]overlay=shortest=1[v] \" -map \"[v]\" -c:v libx264 -c:a aac -ar 48000 -b:a 160k -strict experimental -f mpegts \"#root/temp/#folder/vid_#num.ts\"");
		}
		private void loadSettingVideo()
        {
			lblFont.Text=Properties.Settings.Default.font.Equals("")?"chọn": Properties.Settings.Default.font;
			fontName = Properties.Settings.Default.font;
			lblFontSize.Text = Properties.Settings.Default.sizeText.ToString();
			fontSize = Properties.Settings.Default.sizeText;// 13
			this.lblColorText.BackColor =Color.FromArgb(Properties.Settings.Default.colorText);
			borderColor = Properties.Settings.Default.colorText; //-16777216
			this.lblColorBorder.BackColor = Color.FromArgb(Properties.Settings.Default.borderColor);
			cboSizeBorder.Value = Properties.Settings.Default.sizeBorder;
			this.cboPositionContent.SelectedIndex = Properties.Settings.Default.positionContent;
			contentPosition = Properties.Settings.Default.positionContent;//1
			this.cboAlignContent.SelectedIndex = Properties.Settings.Default.alignContent;
			contentAlign = Properties.Settings.Default.alignContent;//1
			this.cboMarginSub.Value = Properties.Settings.Default.marginSub;
			distanceMarginSub = Properties.Settings.Default.marginSub;//20
			this.cboSubInImg.Value = Properties.Settings.Default.wordInImage;
			checkBoxBg.Checked = Properties.Settings.Default.useBackGround;
			useBackground = Properties.Settings.Default.useBackGround;  
			isSub.Checked = Properties.Settings.Default.isSub;// có sử dụng sub? // tí a check lại cái này
			minWordSub = 15;                                            // số từ tuối thiểu cho 1 đoạn text (ít hơn bỏ qua)
			useSub= Properties.Settings.Default.isSub;                                          // sử dụng nền mờ (em để ý xem video sẽ có nền mờ sau ảnh trong video)
			chiaChinhXac = false;                                          // à,... cái này là có chia theo dấu phẩy hoặc chấm hay không hay lấy theo

	}
		public void saveSettingVideo()
        {
			Font font = this.fontDialog1.Font;
			Properties.Settings.Default.font = this.lblFont.Text;
			Properties.Settings.Default.sizeText = (int) font.Size;
			Properties.Settings.Default.colorText = this.lblColorText.BackColor.ToArgb();
			Properties.Settings.Default.borderColor = this.lblColorBorder.BackColor.ToArgb();
			Properties.Settings.Default.sizeBorder = (int)this.cboSizeBorder.Value;
			Properties.Settings.Default.positionContent = this.cboPositionContent.SelectedIndex;
			Properties.Settings.Default.alignContent = this.cboAlignContent.SelectedIndex;
			Properties.Settings.Default.marginSub =(int) this.cboMarginSub.Value;
			Properties.Settings.Default.wordInImage = (int)this.cboSubInImg.Value;
			Properties.Settings.Default.useBackGround = checkBoxBg.Checked;
			Properties.Settings.Default.isSub = isSub.Checked;
			Properties.Settings.Default.Save();
		}
        private void label18_Click_1(object sender, EventArgs e)
        {

        }

        private void label19_Click(object sender, EventArgs e)
        {

        }

        private void lblFont_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
			DialogResult dialogResult = this.fontDialog1.ShowDialog();
			bool flag = dialogResult == DialogResult.OK;
			if (flag)
			{
				Font font = this.fontDialog1.Font;
				this.lblFont.Text = font.Name;
				this.lblFontSize.Text = font.Size.ToString().Split(new char[]
				{
					'.',
					','
				})[0];
				this.lblFont.Font = new Font(font.Name, 8.25f);
				this.fontName = font.Name;
				this.fontSize = (int)font.Size;

			}
		}

		private void numericUpDown3_ValueChanged(object sender, EventArgs e)
		{

		}
        private void lblColorText_Click(object sender, EventArgs e)
        {
			DialogResult dialogResult = this.colorDialog1.ShowDialog();
			bool flag = dialogResult == DialogResult.OK;
			if (flag)
			{
				this.lblColorText.BackColor = this.colorDialog1.Color;
			}
		}

        private void lblColorBorder_Click(object sender, EventArgs e)
        {
			DialogResult dialogResult = this.colorDialog2.ShowDialog();
			bool flag = dialogResult == DialogResult.OK;
			if (flag)
			{
				this.lblColorBorder.BackColor = this.colorDialog2.Color;
			}

		}

        private void btnSave1_Click(object sender, EventArgs e)
        {
			saveSettingVideo();

		}

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
			if (this.checkBox1.Checked == true)
			{
				panel3.Visible = true;
			}
			else
			{
				panel3.Visible = false;
			}
		}

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
			bool flag = this.cboTemplate.SelectedIndex != 0;
			if (flag)
			{
				this.panelName.Visible = false;
				string[] i = File.ReadAllLines("tpl/" + this.cboTemplate.SelectedItem + ".tp");
				this.txtLinkHome.Text = i[0];
				this.txtLinkCategory.Text = i[1];
				this.txtCSSLink.Text = i[2];
				this.txtCSSTitle.Text = i[3];
				this.txtCSSContent.Text = i[4];
				this.txtCSSRemove.Text = i[5];
				this.txtTextRemove.Text = i[6];
				this.txtIgnoreTitle.Text = i[7];
			}
			else
			{
				this.panelName.Visible = true;
				this.txtLinkHome.Clear();
				this.txtLinkCategory.Clear();
				this.txtCSSLink.Clear();
				this.txtCSSTitle.Clear();
				this.txtCSSContent.Clear();
				this.txtCSSRemove.Clear();
				this.txtTextRemove.Clear();
				this.txtIgnoreTitle.Clear();
			}
		}

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }

        private void label17_Click(object sender, EventArgs e)
        {

        }

        private void label18_Click(object sender, EventArgs e)
        {

        }

        private void label26_Click(object sender, EventArgs e)
        {

        }
    }
}
