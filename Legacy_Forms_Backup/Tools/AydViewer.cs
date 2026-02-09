using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Albatross.Level5.Binary;

namespace Albatross.Forms.Tools
{
    public partial class AydViewer : Form
    {
        private ResourceData _currentData;
        private string _loadedFileName;

        public AydViewer()
        {
            InitializeComponent();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "AYD Files (*.ayd)|*.ayd|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadFile(ofd.FileName);
                }
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                lblStatus.Text = $"Loading {Path.GetFileName(path)}...";
                Application.DoEvents();

                byte[] data = File.ReadAllBytes(path);
                var loader = new AydLoader();

                // LoadAYD recursively handles unpacking
                _currentData = loader.LoadAYD(data);
                _loadedFileName = Path.GetFileNameWithoutExtension(path);

                UpdateFileList();
                lblStatus.Text = $"Loaded {_currentData.Files.Count} files.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error.";
            }
        }

        private void UpdateFileList()
        {
            fileListBox.Items.Clear();

            if (_currentData != null)
            {
                // 2. 모든 파일 목록에 추가
                int firstPngIndex = -1;
                for (int i = 0; i < _currentData.Files.Count; i++)
                {
                    var file = _currentData.Files[i];
                    string ext = file.Extension.ToLower();

                    fileListBox.Items.Add(file.FileName);

                    // 첫 번째 PNG 파일 인덱스 기억
                    if (firstPngIndex == -1 && (ext == ".png" || file.FileName.EndsWith("_o.png", StringComparison.OrdinalIgnoreCase)))
                    {
                        firstPngIndex = i;
                    }
                }

                // 3. 편의성: 첫 번째 PNG 선택 (없으면 0번)
                if (fileListBox.Items.Count > 0)
                {
                    fileListBox.SelectedIndex = firstPngIndex != -1 ? firstPngIndex : 0;
                }
            }
        }

        // byte[] 데이터를 Image 객체로 변환하는 함수
        private Image ByteToImage(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            try
            {
                // 메모리 스트림을 열어서 이미지를 생성합니다.
                // Note: Image.FromStream requires the stream to stay open for the lifetime of the Image.
                // However, creating a new Bitmap from that image copies the data and allows closing the stream.
                using (var ms = new MemoryStream(data))
                {
                    using (var tempImage = Image.FromStream(ms))
                    {
                        // 3/4 크기로 리사이즈 (기존 3/8보다 2배 더 크게 -> 6/8 = 3/4)
                        // 최소 1x1 크기는 유지
                        int newWidth = Math.Max(1, (tempImage.Width * 3) / 4);
                        int newHeight = Math.Max(1, (tempImage.Height * 3) / 4);

                        return new Bitmap(tempImage, new Size(newWidth, newHeight));
                    }
                }
            }
            catch
            {
                return null; // 이미지가 아니거나 깨진 경우
            }
        }

        private void fileListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fileListBox.SelectedIndex == -1 || _currentData == null) return;

            // 1. 선택된 파일 이름 가져오기
            string selectedFileName = fileListBox.SelectedItem.ToString();

            // 2. 리소스 데이터에서 해당 파일 찾기
            var fileData = _currentData.Files
                .FirstOrDefault(f => f.FileName == selectedFileName);

            if (fileData != null)
            {
                // 3. 이미지를 변환해서 PictureBox에 띄우기
                // 기존 이미지 메모리 해제
                if (previewBox.Image != null)
                {
                    var oldImage = previewBox.Image;
                    previewBox.Image = null;
                    oldImage.Dispose();
                }

                // 이미지 확장자인 경우에만 로드 시도
                string ext = fileData.Extension.ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                {
                    previewBox.Image = ByteToImage(fileData.Data);
                }
            }
        }

        private void btnExportAll_Click(object sender, EventArgs e)
        {
            if (_currentData == null || _currentData.Files.Count == 0)
            {
                MessageBox.Show("No files to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string baseDir = Path.Combine(fbd.SelectedPath, _loadedFileName + "_dump");
                        if (!Directory.Exists(baseDir))
                            Directory.CreateDirectory(baseDir);

                        int count = 0;
                        foreach (var file in _currentData.Files)
                        {
                            string safeName = file.FileName;
                            safeName = safeName.TrimStart('/', '\\');

                            string fullPath = Path.Combine(baseDir, safeName);
                            string dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                            File.WriteAllBytes(fullPath, file.Data);
                            count++;
                        }

                        MessageBox.Show($"Exported {count} files to:\n{baseDir}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exporting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void btnNativeExtract_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder containing .ayd files for fast batch extraction.";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    var bridge = new Albatross.Core.EngineBridge();
                    bridge.StartExtractionAsync(fbd.SelectedPath);
                }
            }
        }
    }
}
