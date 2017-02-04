using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
