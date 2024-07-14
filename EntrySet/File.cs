using System;
using System.Collections.Generic;

namespace PK2Reader.EntrySet
{
    public class File
    {
        public string Name { get; set; }
        public long Position { get; set; }
        public uint Size { get; set; }
        public Folder ParentFolder { get; set; }

        public File()
        {
            Name = string.Empty;
            ParentFolder = new Folder();
        }
    }
}
