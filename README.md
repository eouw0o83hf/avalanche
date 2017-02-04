`avalanche`
=========
`avalanche` is a highly specialized agent for backing up photos from Adobe Lightroom to Amazon Glacier. Given a Lightroom catalog, `avalanche` determines which files have been added to any collections and backs those files up to Amazon Glacier.

`avalanche` uses a locally-stored sqlite database to keep track of which files it has sent to Glacier. Since Glacier is designed primarily for storing data, it is not feasible to directly compare local data against Glacier; thus, the local database is critical for determining which files need to be backed up to Glacier. It is recommended to store this database in an automatically backed-up location, such as a Dropbox directory.

# Running Avalanche
Avalanche runs on .NET Core, so first download and install the latest version.

To execute, `cd` into `src/Avalanche` in a terminal, then execute `dotnet run -- [params]` - the `--` after `run` is there to differentiat parameters sent to the runtime vs those sent to the application.

## Parameters

    `-h`	 	Help
    `-c`		Configuration file path. Required to start. **Required.**
    `-t`		Test mode - reads files and executes without writing to Glacier or Avalanche state.

Example:

    $ dotnet run -- -c=/user/myuser/dropbox/avalancheconfig.json

## JSON Config File
`avalanche` loads configuration parameters from a json file.

Currently, the configuration file can handle one Lightroom Catalog at a time. This will be addressed in a future release.

Format:

	{
		"Glacier":
		{	
			"AccountId":"-",
			"AccessKeyId":"AMAZON-ACCESS-KEY",
			"Region":"us-east-1",
			"SecretAccessKey":"SECRET_ACCESS_KEY",
			"SnsTopicId":"arn:aws:sns:us-east-1:idnumber:email",
			"VaultName":"Lightroom-Raws"
		},	
		"Avalanche":
		{
			"CatalogFilePath":"X:\\Pictures\\Catalogs\\DesktopCatalog0-2.lrcat",
			"AvalancheFilePath":"X:\\Dropbox\\Avalanche\\avalanche.sqlite"
		}
	}

## AWS Keys
You can get these out of IAM in the AWS Console. Make sure that the IAM user that they came from has read/write permission to Glacier. It will need to read and create Vaults, as well as read and create Archives within those Vaults.

## Avalanche State File
`AvalancheFilePath` in the config file points to state which Avalanche uses to determine which pictures have been backed up and which haven't. It's not necessary to initialize the file (the config can point to a nonexistent file as long as the directory exists - it will be initialized).

It is necessary to use the same Avalanche file across executions, because it is how Avalanche knows what files have been uploaded and which haven't.
 
# Backup Files
`avalanche` sends backup files to Glacier in a composite, compressed format. Since Glacier storage strips files of all data, including name, the files need to be self-contained. Each file is 7zipped, and contains the original image file and a text document containing a json description of the original file and its relation to its original catalog.