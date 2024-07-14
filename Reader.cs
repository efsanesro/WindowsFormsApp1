using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using PK2Reader.EntrySet;
using PK2Reader.Framework;

namespace PK2Reader
{
    public class Reader : IDisposable
    {
        private Blowfish m_Blowfish = new Blowfish();
        private long m_Size;
        public long Size { get { return m_Size; } }
        private byte[] m_Key;
        public byte[] Key { get { return m_Key; } }
        private string m_Key_Ascii = string.Empty;
        public string ASCIIKey { get { return m_Key_Ascii; } }
        private sPk2Header m_Header;
        public sPk2Header Header { get { return m_Header; } }
        private List<sPk2EntryBlock> m_EntryBlocks = new List<sPk2EntryBlock>();
        public List<sPk2EntryBlock> EntryBlocks { get { return m_EntryBlocks; } }
        private List<PK2Reader.EntrySet.File> m_Files = new List<PK2Reader.EntrySet.File>();
        public List<PK2Reader.EntrySet.File> Files { get { return m_Files; } }
        private List<Folder> m_Folders = new List<Folder>();
        public List<Folder> Folders { get { return m_Folders; } }
        private string m_Path;
        public string Path { get { return m_Path; } }
        private Folder m_CurrentFolder;
        private Folder m_MainFolder;
        private FileStream m_FileStream;

        public Reader(string FileName, string Key)
        {
            if (!System.IO.File.Exists(FileName))
                throw new Exception("File not found");
            else
            {
                m_Path = FileName;
                m_Key = GenerateFinalBlowfishKey(Key);
                m_Key_Ascii = Key;

                m_FileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                m_Size = m_FileStream.Length;

                m_Blowfish.Initialize(m_Key);
                BinaryReader reader = new BinaryReader(m_FileStream);
                m_Header = (sPk2Header)BufferToStruct(reader.ReadBytes(256), typeof(sPk2Header));
                m_CurrentFolder = new Folder
                {
                    Name = FileName,
                    Files = new List<PK2Reader.EntrySet.File>(),
                    SubFolders = new List<Folder>()
                };

                m_MainFolder = m_CurrentFolder;
                Read(reader.BaseStream.Position);
            }
        }

        private static byte[] GenerateFinalBlowfishKey(string ascii_key)
        {
            return GenerateFinalBlowfishKey(ascii_key, new byte[] { 0x03, 0xF8, 0xE4, 0x44, 0x88, 0x99, 0x3F, 0x64, 0xFE, 0x35 });
        }

        private static byte[] GenerateFinalBlowfishKey(string ascii_key, byte[] base_key)
        {
            byte ascii_key_length = (byte)ascii_key.Length;
            if (ascii_key_length > 56) ascii_key_length = 56;

            byte[] a_key = Encoding.ASCII.GetBytes(ascii_key);
            byte[] b_key = new byte[56];
            Array.ConstrainedCopy(base_key, 0, b_key, 0, base_key.Length);

            byte[] bf_key = new byte[ascii_key_length];
            for (byte x = 0; x < ascii_key_length; ++x)
            {
                bf_key[x] = (byte)(a_key[x] ^ b_key[x]);
            }

            return bf_key;
        }

        public void ExtractFile(PK2Reader.EntrySet.File File, string OutputPath)
        {
            byte[] data = GetFileBytes(File);
            using (var stream = new FileStream(OutputPath, FileMode.OpenOrCreate))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(data);
            }
        }

        public void ExtractFile(string Name, string OutputPath)
        {
            byte[] data = GetFileBytes(Name);
            using (var stream = new FileStream(OutputPath, FileMode.OpenOrCreate))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(data);
            }
        }

        public string GetFileExtension(PK2Reader.EntrySet.File File)
        {
            int Offset = File.Name.LastIndexOf('.');
            return File.Name.Substring(Offset);
        }

        public string GetFileExtension(string Name)
        {
            if (FileExists(Name))
            {
                int Offset = Name.LastIndexOf('.');
                return Name.Substring(Offset);
            }
            else
                throw new Exception("The file does not exist");
        }

        public List<PK2Reader.EntrySet.File> GetRootFiles()
        {
            return m_MainFolder.Files;
        }

        public List<Folder> GetRootFolders()
        {
            return m_MainFolder.SubFolders;
        }

        public List<PK2Reader.EntrySet.File> GetFiles(string ParentFolder)
        {
            return m_Files.Where(file => file.ParentFolder.Name == ParentFolder).ToList();
        }

        public List<Folder> GetSubFolders(string ParentFolder)
        {
            return m_Folders.FirstOrDefault(folder => folder.Name == ParentFolder)?.SubFolders ?? new List<Folder>();
        }

        public bool FileExists(string Name)
        {
            return m_Files.Any(item => item.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] GetFileBytes(string Name)
        {
            var file = m_Files.FirstOrDefault(item => item.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                using (var reader = new BinaryReader(m_FileStream, Encoding.UTF8, true))
                {
                    reader.BaseStream.Position = file.Position;
                    return reader.ReadBytes((int)file.Size);
                }
            }
            return null;
        }

        public byte[] GetFileBytes(PK2Reader.EntrySet.File File)
        {
            return GetFileBytes(File.Name);
        }

        public string GetFileText(string Name)
        {
            var tempBuffer = GetFileBytes(Name);
            if (tempBuffer != null)
            {
                using (var txtReader = new StreamReader(new MemoryStream(tempBuffer)))
                {
                    return txtReader.ReadToEnd();
                }
            }
            else
            {
                Console.WriteLine($"Dosya bulunamadı: {Name}");
            }
            return null;
        }

        public string GetFileText(PK2Reader.EntrySet.File File)
        {
            return GetFileText(File.Name);
        }

        public Stream GetFileStream(string Name)
        {
            return new MemoryStream(GetFileBytes(Name));
        }

        public Stream GetFileStream(PK2Reader.EntrySet.File File)
        {
            return GetFileStream(File.Name);
        }

        public List<string> GetFileNames()
        {
            return m_Files.Select(file => file.Name).ToList();
        }

        private void Read(long Position)
        {
            BinaryReader reader = new BinaryReader(m_FileStream);
            reader.BaseStream.Position = Position;
            List<Folder> folders = new List<Folder>();
            sPk2EntryBlock entryBlock = (sPk2EntryBlock)BufferToStruct(m_Blowfish.Decode(reader.ReadBytes(Marshal.SizeOf(typeof(sPk2EntryBlock)))), typeof(sPk2EntryBlock));

            for (int i = 0; i < 20; i++)
            {
                sPk2Entry entry = entryBlock.Entries[i];
                switch (entry.Type)
                {
                    case 0: // Null Entry
                        break;
                    case 1: // Folder 
                        if (entry.Name != "." && entry.Name != "..")
                        {
                            Folder folder = new Folder
                            {
                                Name = entry.Name,
                                Position = BitConverter.ToInt64(entry.g_Position, 0),
                                ParentFolder = m_CurrentFolder
                            };
                            Console.WriteLine($"Klasör: {folder.Name}, Konum: {folder.Position}");
                            folders.Add(folder);
                            m_Folders.Add(folder);
                            m_CurrentFolder.SubFolders.Add(folder);
                        }
                        break;
                    case 2: // File
                        PK2Reader.EntrySet.File file = new PK2Reader.EntrySet.File
                        {
                            Position = entry.Position,
                            Name = entry.Name,
                            Size = entry.Size,
                            ParentFolder = m_CurrentFolder
                        };
                        Console.WriteLine($"Dosya: {file.Name}, Konum: {file.Position}, Boyut: {file.Size}");
                        m_Files.Add(file);
                        m_CurrentFolder.Files.Add(file);
                        break;
                }
            }

            if (entryBlock.Entries[19].NextChain != 0)
            {
                Read(entryBlock.Entries[19].NextChain);
            }

            foreach (Folder folder in folders)
            {
                m_CurrentFolder = folder;
                if (folder.Files == null)
                {
                    folder.Files = new List<PK2Reader.EntrySet.File>();
                }
                if (folder.SubFolders == null)
                {
                    folder.SubFolders = new List<Folder>();
                }
                Read(folder.Position);
            }
        }

        private object BufferToStruct(byte[] buffer, Type returnStruct)
        {
            IntPtr pointer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, pointer, buffer.Length);
            object result = Marshal.PtrToStructure(pointer, returnStruct);
            Marshal.FreeHGlobal(pointer);
            return result;
        }

        [StructLayout(LayoutKind.Sequential, Size = 256)]
        public struct sPk2Header
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)]
            public string Name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Version;
            [MarshalAs(UnmanagedType.I1, SizeConst = 1)]
            public byte Encryption;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Verify;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 205)]
            public byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Size = 128)]
        public struct sPk2Entry
        {
            [MarshalAs(UnmanagedType.I1)]
            public byte Type; // files are 2, folders are 1, null entries are 0
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 81)]
            public string Name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] AccessTime;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] CreateTime;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] ModifyTime;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] g_Position;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            private byte[] m_Size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            private byte[] m_NextChain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Padding;

            public long NextChain { get { return BitConverter.ToInt64(m_NextChain, 0); } }
            public long Position { get { return BitConverter.ToInt64(g_Position, 0); } }
            public uint Size { get { return BitConverter.ToUInt32(m_Size, 0); } }
        }

        [StructLayout(LayoutKind.Sequential, Size = 2560)]
        public struct sPk2EntryBlock
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public sPk2Entry[] Entries;
        }

        public List<string> ListAllFiles()
        {
            foreach (var file in m_Files)
            {
                Console.WriteLine($"Dosya: {file.Name}, Yol: {GetFilePath(file)}");
            }
            return m_Files.Select(file => GetFilePath(file)).ToList();
        }

        public void ListAllFilesWithPaths()
        {
            foreach (var file in m_Files)
            {
                string filePath = GetFilePath(file);
                Console.WriteLine($"Dosya: {file.Name}, Yol: {filePath}");
            }
        }

        public void ListAllFoldersWithPaths()
        {
            foreach (var folder in m_Folders)
            {
                string folderPath = GetFolderPath(folder);
                Console.WriteLine($"Klasör: {folder.Name}, Yol: {folderPath}");
            }
        }
        private string GetFolderPath(Folder folder)
        {
            string path = folder.Name;
            Folder parent = folder.ParentFolder;
            while (parent != null)
            {
                path = parent.Name + "\\" + path;
                parent = parent.ParentFolder;
            }
            return path;
        }
        private string GetFilePath(PK2Reader.EntrySet.File file)
        {
            string path = file.Name;
            Folder parent = file.ParentFolder;
            while (parent != null && parent.Name != m_Path) // Change this condition
            {
                path = parent.Name + "/" + path;
                parent = parent.ParentFolder;
            }
            return path;
        }


        public string GetFileTextIgnoreCase(string path)
        {
            string normalizedPath = NormalizePath(path);
            Console.WriteLine($"Aranan dosya yolu: {normalizedPath}");

            foreach (var file in m_Files)
            {
                string filePath = NormalizePath(GetFilePath(file));
                Console.WriteLine($"Kontrol edilen dosya yolu: {filePath}");
                if (string.Equals(filePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Dosya bulundu: {filePath}");
                    return GetFileText(file.Name);
                }
            }

            Console.WriteLine("Dosya bulunamadı.");
            return null;
        }

        public byte[] GetFileBytesIgnoreCase(string path)
        {
            string normalizedPath = NormalizePath(path);
            Console.WriteLine($"Aranan dosya yolu: {normalizedPath}");

            foreach (var file in m_Files)
            {
                string filePath = NormalizePath(GetFilePath(file));
                Console.WriteLine($"Kontrol edilen dosya yolu: {filePath}");
                if (string.Equals(filePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Dosya bulundu: {filePath}");
                    return GetFileBytes(file.Name);
                }
            }

            Console.WriteLine("Dosya bulunamadı.");
            return null;
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/').ToLower();
        }


        public List<string> SearchInFiles(string searchTerm, string[] filePatterns)
        {
            List<string> resultLines = new List<string>();

            foreach (var pattern in filePatterns)
            {
                var files = m_Files.Where(f => f.Name.StartsWith(pattern) && f.Name.EndsWith(".txt")).ToList();

                foreach (var file in files)
                {
                    var content = GetFileText(file.Name);
                    if (content != null)
                    {
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            if (line.Contains(searchTerm))
                            {
                                resultLines.Add(line);
                            }
                        }
                    }
                }
            }

            return resultLines;
        }

        public List<string> ListAllFolders()
        {
            foreach (var folder in m_Folders)
            {
                Console.WriteLine($"Klasör: {folder.Name}, Konum: {folder.Position}");
            }
            return m_Folders.Select(folder => folder.Name).ToList();
        }

        public void Dispose()
        {
            m_CurrentFolder = null;
            m_EntryBlocks = null;
            m_Files = null;
            m_FileStream = null;
            m_Folders = null;
            m_Key = null;
            m_Key_Ascii = null;
            m_MainFolder = null;
            m_Path = null;
            m_Size = 0;
        }
    }
}
