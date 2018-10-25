<Query Kind="Program" />

void Main()
{
	var appExeName = "MicSwitch.exe";
	var appName = Path.GetFileNameWithoutExtension(appExeName);
	var scriptDir = Path.GetDirectoryName(Util.CurrentQueryPath);
	var homeDir = Path.Combine(scriptDir, "Sources");

	var nuspecFileName = $"{appName}.nuspec";
	var nuspecFilePath = Path.Combine(homeDir, nuspecFileName);
	var version = GetSpecVersion(nuspecFilePath);

	var nupkgFileName = $@"{appName}.{version}.nupkg";
	var nupkgFilePath = Path.Combine(scriptDir, nupkgFileName);

	var releasesFolderName = "Releases";
	var squirrelPath = Path.Combine(homeDir, $@"packages\squirrel.windows.1.0.2\tools\Squirrel.exe");
	var squirrelLogPath = Path.Combine(Path.GetDirectoryName(squirrelPath), "SquirrelSetup.log");

	if (File.Exists(squirrelLogPath))
	{
		File.Delete(squirrelLogPath.Dump("Removing Squirrel log..."));
	}

	new { version, homeDir, nupkgFilePath }.Dump("Running Releasify...");
	var squirrelArgs = new { Path = squirrelPath, Args = $"--releasify=\"{nupkgFilePath}\"", Log = squirrelLogPath }.Dump("Squirrel");
	
	Util.Cmd(squirrelArgs.Path, squirrelArgs.Args, false);

	if (File.Exists(squirrelLogPath))
	{
		File.ReadAllText(squirrelLogPath).Dump("Squirrel execution log");
	}

	var sourceReleasesFolderPath = Path.Combine(Path.GetDirectoryName(squirrelPath), releasesFolderName);
	var targetReleasesFolderPath = Path.Combine(scriptDir, releasesFolderName);

	if (Directory.Exists(targetReleasesFolderPath))
	{
		targetReleasesFolderPath.Dump("Target directory exists, removing it");
		Directory.Delete(targetReleasesFolderPath);
	}

	new { sourceReleasesFolderPath, targetReleasesFolderPath }.Dump("Moving 'Releases' folder...");
	Directory.Move(sourceReleasesFolderPath, targetReleasesFolderPath);

	var sourceSetupFileName = Path.Combine(targetReleasesFolderPath, "Setup.exe");
	var targetSetupFileName = Path.Combine(targetReleasesFolderPath, $"{appName}Setup.{version}.exe");

	new { sourceSetupFileName, targetSetupFileName }.Dump("Renaming Setup.exe");
	File.Copy(sourceSetupFileName, targetSetupFileName);

	var squirrelLogFilePath = Path.Combine(Path.GetDirectoryName(squirrelPath), "SquirrelSetup.log");
	var squirrelLog = File.Exists(squirrelLogFilePath) ? File.ReadAllText(squirrelLogFilePath) : $"Squirrel log file does not exist at path {squirrelLogFilePath}";
	squirrelLog.Dump("Squirrel execution log");
}


private static string GetSpecVersion(string nuspecFilePath)
{
	new[] { nuspecFilePath }.Dump("Reading version from .nuspec file...");
	var nuspecDocument = XElement.Load(nuspecFilePath);
	var ns = nuspecDocument.GetDefaultNamespace();

	var version = nuspecDocument.Descendants(ns + "metadata").Single().Descendants(ns + "version").Single().Value;

	return version;
}

// Define other methods and classes here