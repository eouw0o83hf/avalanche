avalanche
=========

Highly specialized agent for backing up photos from Adobe Lightroom to Amazon Glacier.

# Parameters


#JSON Config File

avalanche can load configuration parameters from a json file. By default, it tries to load from avalanche.json in the "My Documents" directory, but you can also specify one with the -c parameter.

## File Format
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
 
