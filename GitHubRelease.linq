<Query Kind="Program" />

void Main()
{
	var appExeName = "MicSwitch.exe";
	var appName = Path.GetFileNameWithoutExtension(appExeName);

	var username = "iXab3r";
	var reponame = "MicSwitch";
	var releasesFolderName = "Releases";
	
	var scriptDir = Path.GetDirectoryName(Util.CurrentQueryPath);
	var homeDir = Path.Combine(scriptDir, appName);
	var toolsDir = Path.Combine(scriptDir, "Tools");
	var grPath = Path.Combine(toolsDir, "github-release.exe");
	var releasesPath = Path.Combine(scriptDir, releasesFolderName);

	var packages = Directory.GetFiles(releasesPath, "*.nupkg");
	packages.Dump("Packages to upload");
	if (packages.Length > 1){
		throw new ApplicationException("Expected single package");
	}
	
	var setupFilePath = Path.Combine(releasesPath, "Setup.exe");
	
	var args = new { 
		scriptDir, 
		grPath,
		setupFilePath, 
		username, 
		reponame, 
		GithubTokenIsSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")) 
	}.Dump("GitHubRelease Arguments");
	if (!args.GithubTokenIsSet){
		$"Trying to read github.token from PasswordManager cache".Dump();
		var linqToken = Util.GetPassword("github.token", noDefaultSave: true);

		if (!string.IsNullOrWhiteSpace(linqToken))
		{
			$"Token is loaded from PasswordManager, length: {linqToken.Length}".Dump();
			Environment.SetEnvironmentVariable("GITHUB_TOKEN", linqToken);
		}
	}
	
	var versionInfo = FileVersionInfo.GetVersionInfo(setupFilePath);
	var version = versionInfo.ProductVersion;
	var versionTag = $"v{version}";
	versionTag.Dump($"{appName} version");
	
	$"Preparing release draft {versionTag}".Dump();
	try
	{
		Util.Cmd(grPath, $"release --user {username} --repo {reponame} --tag {versionTag} --draft", false);
	}
	catch (CommandExecutionException ex)
	{
		ex.Dump("Failed to create release, trying to cleanup...");
		Util.Cmd(grPath, $"delete --user {username} --repo {reponame} --tag {versionTag}", false);
	}

	$"Uploading releases file".Dump();
	Util.Cmd(grPath, $"upload --user {username} --repo {reponame} --tag {versionTag} --name \"RELEASES\" --file \"{Path.Combine(releasesPath, "RELEASES")}\" --replace", false);

	$"Uploading binaries".Dump();
	Util.Cmd(grPath, $"upload --user {username} --repo {reponame} --tag {versionTag} --name \"{appName}Setup.{version}.exe\" --file \"{setupFilePath}\" --replace", false);
	
	foreach (var fileName in packages)
	{
		fileName.Dump("Uploading...");
		Util.Cmd(grPath, $"upload --user {username} --repo {reponame} --tag {versionTag} --file \"{fileName}\" --name \"{Path.GetFileName(fileName)}\" --replace", false);
	}
}