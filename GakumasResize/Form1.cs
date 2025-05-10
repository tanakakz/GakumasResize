using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Storage.Xps;

namespace GakumasResize
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            ReloadDisplayList();
        }

        private void ReloadDisplayList()
        {
            comboBoxDisplay.Items.Clear();
            foreach (var screen in Screen.AllScreens)
            {
                comboBoxDisplay.Items.Add(screen);
            }
        }

        private void buttonResize_Click(object sender, EventArgs e)
        {
            var screen = comboBoxDisplay.SelectedItem as Screen;
            if (screen == null)
            {
                MessageBox.Show(@"ディスプレイが指定されていません。先に学マスを表示するディスプレイを選択してください。", @"GakumasResize", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            const string targetWindowName = "gakumas";
            var process = Process.GetProcessesByName(targetWindowName).FirstOrDefault();
            if (process == null)
            {
                MessageBox.Show(@"学マスのウィンドウが見つかりませんでした。学マスが起動していることを確認してください。", @"GakumasResize", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var width = (int)numericUpDownWidth.Value;
            var height = (int)numericUpDownHeight.Value;

            var hWnd = (HWND)process.MainWindowHandle;
            PInvoke.GetWindowRect(hWnd, out var windowRect);
            PInvoke.GetClientRect(hWnd, out var clientRect);
            var screenPoint = new Point();
            PInvoke.ClientToScreen(hWnd, ref screenPoint);
            var windowWidth = windowRect.right - windowRect.left;
            var clientWidth = clientRect.right - clientRect.left;
            var frameWidth = windowWidth - clientWidth;
            var windowHeight = windowRect.bottom - windowRect.top;
            var clientHeight = clientRect.bottom - clientRect.top;
            var frameHeight = windowHeight - clientHeight;

            var newPoint = new Point
            {
                X = screen.Bounds.X,
                Y = screen.Bounds.Y
            };
            if (radioButtonPosCenter.Checked)
            {
                // 中央寄せ
                newPoint.X += (screen.Bounds.Width - width) / 2;
                newPoint.Y += (screen.Bounds.Height - height) / 2;
            } else
            {
                if (radioButtonPosRightBottom.Checked || radioButtonPosLeftBottom.Checked)
                {
                    // 下寄せ
                    newPoint.Y += screen.Bounds.Height - height;
                }
                if (radioButtonPosRightTop.Checked || radioButtonPosRightBottom.Checked)
                {
                    // 右寄せ
                    newPoint.X += screen.Bounds.Width - width;
                }
            }
            newPoint.X += windowRect.left - screenPoint.X;
            newPoint.Y += windowRect.top - screenPoint.Y;

            // 違うDPIのモニタからウィンドウを移動すると学マス側？でDPIの差分からのウィンドウサイズ補正がかかる
            // ので2回リサイズ処理を行う
            PInvoke.MoveWindow(hWnd, newPoint.X, newPoint.Y, width + frameWidth, height + frameHeight, true);
            PInvoke.MoveWindow(hWnd, newPoint.X, newPoint.Y, width + frameWidth, height + frameHeight, true);
        }

        private void SetResolution(int width, int height)
        {
            numericUpDownWidth.Value = width;
            numericUpDownHeight.Value = height;
        }

        private void buttonSetResTo1280_Click(object sender, EventArgs e)
        {
            SetResolution(1280, 720);
        }

        private void buttonSetResTo1920_Click(object sender, EventArgs e)
        {
            SetResolution(1920, 1080);
        }

        private void buttonSetResTo2560_Click(object sender, EventArgs e)
        {
            SetResolution(2560, 1440);
        }

        private void buttonSetResTo3840_Click(object sender, EventArgs e)
        {
            SetResolution(3840, 2160);
        }

        private void buttonSetResToDisplay_Click(object sender, EventArgs e)
        {
            var screen = comboBoxDisplay.SelectedItem as Screen;
            if (screen == null)
            {
                MessageBox.Show(@"ディスプレイが指定されていません。先に学マスを表示するディスプレイを選択してください", @"GakumasResize", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SetResolution(screen.Bounds.Width, screen.Bounds.Height);
        }
        
        private string GetScreenshotFolder()
        {
            try
            {
                var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures, 
                    Environment.SpecialFolderOption.Create);
                var gakumasResizePicturesFolder = Path.Combine(picturesFolder, "GakumasResize");
                
                if (!Directory.Exists(gakumasResizePicturesFolder))
                {
                    Directory.CreateDirectory(gakumasResizePicturesFolder);
                }
                
                return gakumasResizePicturesFolder;
            }
            catch (Exception ex)
            {
                throw new IOException("スクリーンショットフォルダの作成に失敗しました。", ex);
            }
        }
        
        private void buttonScreenShot_Click(object sender, EventArgs e)
        {
            try
            {
                var gakumasResizePicturesFolder = GetScreenshotFolder();
                const string targetWindowName = "gakumas";
                var process = Process.GetProcessesByName(targetWindowName).FirstOrDefault();
                if (process == null)
                {
                    MessageBox.Show(@"学マスのウィンドウが見つかりませんでした。", @"エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var hWnd = (HWND)process.MainWindowHandle;
                PInvoke.GetClientRect(hWnd, out var clientRect);
                // 絶妙に黒に近い色がなんか透過色になってしまう (GIFじゃねーんだぞ)
                // とりあえず 24bpp にすることで #000000 になるので透過されてしまっているよりは目立ちにくいが
                // よ～く見るとわかってしまうのでそのうちなんとかしたい
                // というかそもそも Windows.Graphics.Capture API を使うべきである (かなり新しい Windows 10 でないと使えないが)
                using var bitmap = new Bitmap(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                var dc = (HDC)graphics.GetHdc();
                try
                {
                    const uint pwRenderFullContent = 2U;
                    if (!PInvoke.PrintWindow(hWnd, dc, PRINT_WINDOW_FLAGS.PW_CLIENTONLY | (PRINT_WINDOW_FLAGS)pwRenderFullContent))
                    {
                        throw new InvalidOperationException("スクリーンショットの取得に失敗しました。");
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(dc);
                }

                byte[] png;
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    png = stream.ToArray();
                }

                string GetNotExistsFileName(string prefix, string suffix, int i = 0)
                {
                    while (true)
                    {
                        if (i > 9) throw new Exception("ファイル多すぎです");
                        var path = i == 0 ? $"{prefix}{suffix}" : $"{prefix}_{i}{suffix}";
                        if (!File.Exists(path)) return path;
                        i = i + 1;
                    }
                }

                Clipboard.SetImage(Image.FromStream(new MemoryStream(png)));
                var path = GetNotExistsFileName($"{gakumasResizePicturesFolder}\\{DateTime.Now:yyyyMMdd_HHmmss}", ".png");
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    stream.Write(png);
                }
                labelScreenShotState.Text = $@"{Path.GetFileName(path)} に保存しました";
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"エラーが発生しました: {ex.Message}", @"エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonOpenScreenShotFolder_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", GetScreenshotFolder());
        }
    }
}