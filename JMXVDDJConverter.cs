using System;
using System.IO;

namespace JMXVDDJConverter
{
    public static class DDJConverter
    {
        public static void ConvertDDJ(byte[] ddjContent, string outputPath)
        {
            using (var ms = new MemoryStream(ddjContent))
            {
                using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    ms.Seek(20, SeekOrigin.Begin);
                    ms.CopyTo(fs);
                }
            }
        }

        public static void ConvertDDSToDDJ(string ddsPath, string outputPath)
        {
            using (var fs = new FileStream(ddsPath, FileMode.Open, FileAccess.Read))
            {
                using (var bw = new BinaryWriter(System.IO.File.Open(outputPath, FileMode.Create)))
                {
                    bw.Write("JMXVDDJ 1000".ToCharArray());
                    bw.Write((int)fs.Length + 8);
                    bw.Write(3); // 3 = Texture
                    fs.CopyTo(bw.BaseStream);
                }
            }
        }
    }
}
