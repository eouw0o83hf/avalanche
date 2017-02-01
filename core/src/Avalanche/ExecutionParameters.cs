using Amazon;
using Mono.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Logging;

namespace Avalanche
{
    public class CommandLineParameters
    {
        public string ConfigFileLocation { get; set; }
        public bool TestMode { get; set; }
    }

    public class ExecutionParameters
    {
        public GlacierParameters Glacier { get; set; }
        public AvalancheParameters Avalanche { get; set; }
        
        // This is read from the command line, the rest is stored in a config file
        [JsonIgnore]
        public CommandLineParameters CommandLineParameters { get; set; }

        public IEnumerable<string> GetValidationErrors()
        {
            return Glacier.GetValidationErrors().Union(Avalanche.GetValidationErrors());
        }
    }

    public static class ExecutionParameterHelpers
    {
        public static CommandLineParameters ResolveConfigFileLocation(string[] args)
        {
            var showHelp = false;
            var testMode = false;
            string configFileLocation = null;
            var options = new OptionSet
            {
                { "c=|config-file=", "REQUIRED: Path/File for Avalanche Config File", a => configFileLocation = a.Trim('"') },
                { "t=|test", "Run in test mode without pushing any files to AWS", a => testMode = a != null },
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
                return null;
            }

            return new CommandLineParameters
            {
                ConfigFileLocation = configFileLocation,
                TestMode = testMode
            };
        }

        public static ExecutionParameters LoadExecutionParameters(CommandLineParameters parameters)
        {
            var serialized = File.ReadAllText(parameters.ConfigFileLocation);
            if(serialized.Contains("CatalongFilePath"))
            {
                throw new Exception("There was a typo in the original config code. Please rename `CatalongFilePath` to `CatalogFilePath` in your config file.");
            }

            var response = JsonConvert.DeserializeObject<ExecutionParameters>(serialized);
            response.CommandLineParameters = parameters;
            return response;
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
            throw new Exception($"Could not parse the value of `{nameof(Region)}` [{Region}] into an Amazon.RegionEndpoint");
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
