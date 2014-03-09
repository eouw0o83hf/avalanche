using Amazon;
using log4net;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche
{
    public class ExecutionParameters
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));
        
        public GlacierParameters Glacier { get; set; }
        public AvalancheParameters Avalanche { get; set; }

        public string ConfigFileLocation { get; set; }

        public static ExecutionParameters Initialize(string[] args)
        {
            var context = new ExecutionParameters
            {
                Glacier = new GlacierParameters(),
                Avalanche = new AvalancheParameters()
            };

            var showHelp = false;
            var options = new OptionSet
            {
                { "gk|glacier-key", "Access Key ID for Amazon Glacier", a => context.Glacier.AccessKeyId = a },
                { "gs|glacier-secret", "Secret Access Key for Amazon Glacier", a => context.Glacier.SecretAccessKey = a },
                { "ga|glacier-account", "Account ID for Amazon Glacier", a => context.Glacier.AccountId = a },
                { "gsns|glacier-sns-topic", "SNS Topic ID for Amazon Glacier Job", a => context.Glacier.SnsTopicId = a },
                { "gv|glacier-vault", "Vault name for Amazon Glacier", a => context.Glacier.VaultName = a },
                { "gr|glacier-region", "Region for Glacier. Options are {APNortheast1, APSoutheast1, APSoutheast2, CNNorth1, EUWest1, SAEast1, USEast1, USGovCloudWest1, USWest1, USWest2}", a => context.Glacier.Region = a },
                { "lc|lightroom-catalog", "Path/File for Lightroom Catalog", a => context.Avalanche.CatalongFilePath = a },
                { "ad|avalanche-db", "Path/File for Avalanche DB", a => context.Avalanche.AvalancheFilePath = a },
                { "c|config-file", "Path/File for Avalanche Config File", a => context.ConfigFileLocation = a },
                { "h|help", "Help", a => showHelp = a != null }
            };

            try
            {
                var extra = options.Parse(args);
                if (extra.Any())
                {
                    _log.Error("Error: following arguments were not understood: (If you need a list, use -h for help)");
                    foreach (var e in extra)
                    {
                        _log.Error(e);
                    }
                    return null;
                }
            }
            catch (OptionException ex)
            {
                _log.Error("Error with arguments: (If you need a list, use -h for help)");
                _log.Error(ex.Message);
                return null;
            }

            return context;
        }
    }

    public class GlacierParameters
    {
        public string AccountId { get; set; }
        public string AccessKeyId { get; set; }
        public string SecretAccessKey { get; set; }

        public string SnsTopicId { get; set; }

        public string Region { get; set; }
        public string VaultName { get; set; }

        public RegionEndpoint GetRegion()
        {
            foreach (var r in RegionEndpoint.EnumerableAllRegions)
            {
                if (r.SystemName.Equals(Region, StringComparison.InvariantCultureIgnoreCase))
                {
                    return r;
                }
            }
            return null;
        }
    }

    public class AvalancheParameters
    {
        public string CatalongFilePath { get; set; }
        public string AvalancheFilePath { get; set; }
    }
}
