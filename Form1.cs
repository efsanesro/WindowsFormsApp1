using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PK2Reader;
using PK2Reader.EntrySet;
using JMXVDDJConverter;

namespace SilkroadItemExporter
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PK2 files (*.pk2)|*.pk2|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            string searchTerm = txtSearchTerm.Text;
            string pk2FilePath = txtFilePath.Text;
            string pk2Password = txtPk2Password.Text;

            if (System.IO.File.Exists(pk2FilePath))
            {
                try
                {
                    using (var reader = new Reader(pk2FilePath, pk2Password))
                    {
                        reader.ListAllFoldersWithPaths();

                        var textDataObjectPath = "server_dep/silkroad/textdata/textdata_object.txt";
                        var textDataObjectContent = reader.GetFileTextIgnoreCase(textDataObjectPath);

                        if (textDataObjectContent != null)
                        {
                            var lines = textDataObjectContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                            listViewItems.Items.Clear();
                            imageList.Images.Clear(); // Önceki resimleri temizle
                            foreach (var line in lines)
                            {
                                if (line.Contains(searchTerm))
                                {
                                    var itemName = ExtractItemName(line);
                                    var ddjPath = FindDDJPath(reader, itemName);
                                    if (!string.IsNullOrEmpty(ddjPath))
                                    {
                                        ListViewItem item = new ListViewItem(ddjPath);
                                        item.Tag = ddjPath; // dosya yolunu saklamak için Tag kullanıyoruz
                                        listViewItems.Items.Add(item);

                                        var image = LoadDDJImage(reader, ddjPath);
                                        if (image != null)
                                        {
                                            imageList.Images.Add(image);
                                            item.ImageIndex = imageList.Images.Count - 1;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Resim yüklenemedi: {ddjPath}");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("textdata_object.txt dosyası bulunamadı.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("PK2 dosyası bulunamadı.");
            }
        }

        private string ExtractItemName(string line)
        {
            var parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return parts[1];
            }
            return string.Empty;
        }

        private string FindDDJPath(Reader reader, string itemName)
        {
            string[] filePatterns = { "itemdata_", "characterdata_" };
            var resultLines = reader.SearchInFiles(itemName, filePatterns);

            foreach (var resultLine in resultLines)
            {
                var fields = resultLine.Split(new[] { '\t' }, StringSplitOptions.None);
                if (fields.Length > 2)
                {
                    var ddjPath = fields.SkipWhile(field => !field.EndsWith(".bsr")).Skip(2).FirstOrDefault();
                    if (!string.IsNullOrEmpty(ddjPath))
                    {
                        return ddjPath;
                    }
                }
            }

            return string.Empty;
        }

        private Image LoadDDJImage(Reader reader, string ddjPath)
        {
            string ddjFullPath = $"icon/{ddjPath.Replace('\\', '/')}";
            Console.WriteLine($"DDJ tam yolu: {ddjFullPath}");

            var ddjContent = reader.GetFileBytesIgnoreCase(ddjFullPath);

            if (ddjContent != null)
            {
                string tempDdsPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(ddjPath) + ".dds");
                DDJConverter.ConvertDDJ(ddjContent, tempDdsPath);

                if (System.IO.File.Exists(tempDdsPath))
                {
                    return LoadDDSFile(tempDdsPath);
                }
                else
                {
                    Console.WriteLine($"DDS dosyası oluşturulamadı: {tempDdsPath}");
                }
            }
            else
            {
                Console.WriteLine($"DDJ dosyası bulunamadı: {ddjFullPath}");
            }

            return null;
        }

        private Bitmap LoadDDSFile(string ddsFilePath)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(System.IO.File.ReadAllBytes(ddsFilePath)))
                {
                    // DDS dosya başlığını oku
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        if (reader.ReadInt32() != 0x20534444) // 'DDS ' imzası
                        {
                            throw new Exception("Invalid DDS file.");
                        }

                        reader.BaseStream.Seek(12, SeekOrigin.Begin); // Dosya formatına göre genişlik ve yükseklik bilgilerini oku
                        int height = reader.ReadInt32();
                        int width = reader.ReadInt32();

                        reader.BaseStream.Seek(128, SeekOrigin.Begin); // DDS başlığını atla ve görüntü verilerini oku
                        byte[] imageData = reader.ReadBytes((int)(reader.BaseStream.Length - 128));

                        // Bitmap oluştur
                        Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = ((height - y - 1) * width + x) * 4;
                                byte a = imageData[index + 3];
                                byte r = imageData[index + 2];
                                byte g = imageData[index + 1];
                                byte b = imageData[index];
                                bitmap.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                            }
                        }

                        return bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading DDS file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }
    }
}
