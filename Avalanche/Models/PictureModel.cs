using System;

namespace Avalanche.Models
{
    public class PictureModel
    {
        public string AbsolutePath { get; set; }
        public string CatalogRelativePath { get; set; }
        public string FileName { get; set; }

        public Guid FileId { get; set; }
        public Guid ImageId { get; set; }

        public int LibraryCount { get; set; }
        public string CopyName { get; set; }
    }
}
