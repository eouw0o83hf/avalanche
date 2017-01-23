`avalanche`
=========
`avalanche` is a highly specialized agent for backing up photos from Adobe Lightroom to Amazon Glacier. Given a Lightroom catalog, `avalanche` determines which files have been added to any collections and backs those files up to Amazon Glacier.

`avalanche` uses a locally-stored sqlite database to keep track of which files it has sent to Glacier. Since Glacier is designed primarily for storing data, it is not feasible to directly compare local data against Glacier; thus, the local database is critical for determining which files need to be backed up to Glacier. It is recommended to store this database in an automatically backed-up location, such as a Dropbox directory.

# Parameters
## Amazon Params
`-gk`		Glacier Access Key ID (always required)

`-gs`		Glacier Secret Access Key (always required)

`-ga`		Glacier Account Name (not required, not recommended to provide unless you know why you're doing it)

`-gsns`		Amazon SNS Topic ID (only required for pulling data)

`-gv`		Glacier Vault Name (always required; it will be created if not already found)

`-gr`		Amazon AWS region (always required), options are {APNortheast1, APSoutheast1, APSoutheast2, CNNorth1, EUWest1, SAEast1, USEast1, USGovCloudWest1, USWest1, USWest2}

## Avalanche Params
`-lc`		Ligthroom Catalog absolute path (required)
`-ad`		Avalanche DB absolute path (required). You should use the same avalanche DB all the time.
`-c`		Avalanche Config File absolute path

# JSON Config File
`avalanche` can load configuration parameters from a json file. By default, it tries to load from `avalanche.json` in the "My Documents" directory, but you can also specify one with the `-c` parameter.

## `avalanche.json` Configuration File Format
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
			"CatalongFilePath":"X:\\Pictures\\Catalogs\\DesktopCatalog0-2.lrcat",
			"AvalancheFilePath":"X:\\Dropbox\\Avalanche\\avalanche.sqlite"
		},
		"ConfigFileLocation":null
	}
 
# Backup Files
`avalanche` sends backup files to Glacier in a composite, compressed format. Since Glacier storage strips files of all data, including name, the files need to be self-contained. Each file is 7zipped, and contains the original image file and a text document containing a json description of the original file and its relation to its original catalog.

# Supported Platforms
Currently, `avalanche` supports only Windows. .NET Core support will be added next such that it will function on any Core-supported operating system.