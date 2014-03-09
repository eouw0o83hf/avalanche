using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche.Models
{
    public class ArchiveModel
    {
        public string ArchiveId { get; set; }
        public HttpStatusCode Status { get; set; }
        public string Location { get; set; }

        public DateTime PostedTimestamp { get; set; }
        public string Metadata { get; set; }
    }
}
