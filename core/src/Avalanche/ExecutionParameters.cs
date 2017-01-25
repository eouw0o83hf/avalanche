using Amazon;
using Mono.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Avalanche
{
    public class ExecutionParameters
    {
        public GlacierParameters Glacier { get; set; }
        public AvalancheParameters Avalanche { get; set; }

        public IEnumerable<string> GetValidationErrors()
        {
            return Glacier.GetValidationErrors().Union(Avalanche.GetValidationErrors());
        }
    }

    public static class ExecutionParameterHelpers
    {
        public static string ResolveConfigFileLocation(string[] args)
        {
            var showHelp = false;
            string configFileLocation = null;
            var options = new OptionSet
            {
                { "c=|config-file=", "REQUIRED: Path/File for Avalanche Config File", a => configFileLocation = a.Trim('"') },
                { "h|help", "Help", a => showHelp = a != null }
            };

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return null;
            }
            else if(string.IsNullOrEmpty(configFileLocation)
                    || !File.Exists(configFileLocation))
            {
                Console.Error.WriteLine("Need to provide a valid config file location");
            }

            return configFileLocation;
        }

        public static ExecutionParameters LoadExecutionParameters(string configFileLocation)
        {
            var serialized = File.ReadAllText(configFileLocation);
            return JsonConvert.DeserializeObject<ExecutionParameters>(serialized);
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
                if (r.SystemName.Equals(Region, StringComparison.CurrentCultureIgnoreCase))
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
        public string CatalogFilePath { get; set; }

        public string AvalancheFilePath { get; set; }

        public IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(CatalogFilePath))
            {
                yield return "Lightroom Catalog file path is required";
            }
            else if (!File.Exists(CatalogFilePath))
            {
                yield return "Could not find a Lightroom Catalog file at path " + CatalogFilePath;
            }

            if (string.IsNullOrWhiteSpace(AvalancheFilePath))
            {
                yield return "Avalanch File path is required";
            }
        }
    }
}
