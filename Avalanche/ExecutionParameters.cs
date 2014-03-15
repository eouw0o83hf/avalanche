using Amazon;
using log4net;
using Mono.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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

        public void Initialize(string[] args)
        {
            if(Glacier == null)
            {
                Glacier = new GlacierParameters();
            }
            if (Avalanche == null)
            {
                Avalanche = new AvalancheParameters();
            }

            var showHelp = false;
            var options = new OptionSet
            {
                { "gk=|glacier-key=", "Access Key ID for Amazon Glacier", a => Glacier.AccessKeyId = a },
                { "gs=|glacier-secret=", "Secret Access Key for Amazon Glacier", a => Glacier.SecretAccessKey = a },
                { "ga=|glacier-account=", "Account ID for Amazon Glacier", a => Glacier.AccountId = a },
                { "gt=|glacier-sns-topic=", "SNS Topic ID for Amazon Glacier Job", a => Glacier.SnsTopicId = a },
                { "gv=|glacier-vault=", "Vault name for Amazon Glacier", a => Glacier.VaultName = a },
                { "gr=|glacier-region=", "Region for Glacier. Options are {APNortheast1, APSoutheast1, APSoutheast2, CNNorth1, EUWest1, SAEast1, USEast1, USGovCloudWest1, USWest1, USWest2}", a => Glacier.Region = a },
                { "lc=|lightroom-catalog=", "Path/File for Lightroom Catalog", a => Avalanche.CatalongFilePath = a },
                { "ad=|avalanche-db=", "Path/File for Avalanche DB", a => Avalanche.AvalancheFilePath = a },
                { "c=|config-file=", "Path/File for Avalanche Config File", a => ConfigFileLocation = a },
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
                    Environment.Exit(0);
                }
            }
            catch (OptionException ex)
            {
                _log.Error("Error with arguments: (If you need a list, use -h for help)");
                _log.Error(ex.Message);
                Environment.Exit(0);
            }

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Error);
            }
        }

        public static ExecutionParameters GetParametersFromArgs(string[] args)
        {
            ExecutionParameters parameters = null;

            // Load from disk if possible
            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultPath = Path.Combine(myDocuments, "avalanche.json");
            if (File.Exists(defaultPath))
            {
                parameters = JsonConvert.DeserializeObject<ExecutionParameters>(File.ReadAllText(defaultPath));
            }

            if (parameters == null)
            {
                parameters = new ExecutionParameters();
            }

            parameters.Initialize(args);
            if (parameters.ConfigFileLocation != null)
            {
                if (!File.Exists(parameters.ConfigFileLocation))
                {
                    _log.FatalFormat("Couldn't find config file specified at location {0}", parameters.ConfigFileLocation);
                    Environment.Exit(0);
                }

                // Ah crap, a different file was specified. Load it instead.
                parameters = JsonConvert.DeserializeObject<ExecutionParameters>(File.ReadAllText(parameters.ConfigFileLocation));
                parameters.Initialize(args);
            }

            return parameters;
        }

        public ICollection<string> GetValidationErrors()
        {
            return Glacier.GetValidationErrors().Union(Avalanche.GetValidationErrors()).ToList();
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

        public IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(AccessKeyId))
            {
                yield return "Glacier Access Key ID is required.";
            }
            if (string.IsNullOrWhiteSpace(SecretAccessKey))
            {
                yield return "Glacier Secret Access Key is required.";
            }

            if (string.IsNullOrWhiteSpace(Region))
            {
                yield return "Glacier Region is required.";
            }
            else if (GetRegion() == null)
            {
                yield return "Could not parse given Glacier Region " + GetRegion();
            }

            if (string.IsNullOrWhiteSpace(VaultName))
            {
                yield return "Glacier Vault Name is required.";
            }
        }
    }

    public class AvalancheParameters
    {
        public string CatalongFilePath { get; set; }
        public string AvalancheFilePath { get; set; }

        public IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(CatalongFilePath))
            {
                yield return "Lightroom Catalog file path is required";
            }
            else if (!File.Exists(CatalongFilePath))
            {
                yield return "Could not find a Lightroom Catalog file at path " + CatalongFilePath;
            }

            if (string.IsNullOrWhiteSpace(AvalancheFilePath))
            {
                yield return "Avalanch File path is required";
            }
        }
    }
}
