
using System.Collections.Generic;
using Avalanche.Models;

namespace Avalanche.Runner
{
    public class AvalancheRunResult
    {
        public ICollection<PictureModel> Successes { get; set; } = new List<PictureModel>();
        public ICollection<PictureModel> Failures { get; set; } = new List<PictureModel>();
    }
}