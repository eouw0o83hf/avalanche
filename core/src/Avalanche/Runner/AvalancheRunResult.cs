
using System.Collections.Generic;
using Avalanche.Models;

namespace Avalanche.Runner
{
    public class AvalancheRunResult
    {
        public ICollection<PictureModel> Successes { get; set; }
        public ICollection<PictureModel> Failures { get; set; }
    }
}