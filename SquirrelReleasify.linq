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
	version.Dump("NuSpec version");

	var nupkgFileName = $@"{appName}.{version}.nupkg";
	var nupkgFilePath = Path.Combine(scriptDir, nupkgFileName);

	var releasesFolderName = "Releases";
	var squirrelPath = Path.Combine(homeDir, $@"packages\squirrel.windows.1.9.0\tools\Squirrel.exe");
	if (!File.Exists(squirrelPath))
	{
		throw new FileNotFoundException($@"Squirrel executable not found '{squirrelPath}' !");
	}
	var squirrelLogPath = Path.Combine(Path.GetDirectoryName(squirrelPath), "SquirrelSetup.log");

	var sourceReleasesFolderPath = Path.Combine(Path.GetDirectoryName(squirrelPath), releasesFolderName);
	var targetReleasesFolderPath = Path.Combine(scriptDir, releasesFolderName);

	if (Directory.Exists(targetReleasesFolderPath))
	{
		targetReleasesFolderPath.Dump("Target directory exists, removing it");
		Directory.Delete(targetReleasesFolderPath, true);
	}

	if (Directory.Exists(sourceReleasesFolderPath))
	{
		sourceReleasesFolderPath.Dump("Squirrel Source directory exists, removing it");
		Directory.Delete(sourceReleasesFolderPath, true);
	}

	if (File.Exists(squirrelLogPath))
	{
		File.Delete(squirrelLogPath.Dump("Removing Squirrel log..."));
	}

	new { version, homeDir, nupkgFilePath }.Dump("Running Releasify...");
	var squirrelArgs = new { Path = squirrelPath, Args = $"--releasify=\"{nupkgFilePath}\"", Log = squirrelLogPath }.Dump("Squirrel");
	
	Util.Cmd(squirrelArgs.Path, squirrelArgs.Args, false);

	var squirrelLogFilePath = Path.Combine(Path.GetDirectoryName(squirrelPath), "SquirrelSetup.log");
	var squirrelLog = File.Exists(squirrelLogFilePath) ? File.ReadAllText(squirrelLogFilePath) : $"Squirrel log file does not exist at path {squirrelLogFilePath}";
	squirrelLog.Dump("Squirrel execution log");

	new { sourceReleasesFolderPath, targetReleasesFolderPath }.Dump("Moving 'Releases' folder...");
	Directory.Move(sourceReleasesFolderPath, targetReleasesFolderPath);

	var sourceSetupFileName = Path.Combine(targetReleasesFolderPath, "Setup.exe");

	var versionInfo = FileVersionInfo.GetVersionInfo(sourceSetupFileName);
	new { versionInfo.ProductName, versionInfo.ProductVersion, versionInfo.FileVersion }.Dump($"{sourceSetupFileName}");

	if (versionInfo.FileVersion != version)
	{
		throw new ApplicationException($"Setup file should have the same FileVersion {version}, got {versionInfo.FileVersion}");
	}

	if (versionInfo.ProductVersion != version)
	{
		throw new ApplicationException($"Setup file should have the same ProductVersion {version}, got {versionInfo.ProductVersion}");
	}

	var targetSetupFileName = Path.Combine(targetReleasesFolderPath, $"{appName}Setup.{version}.exe");

	new { sourceSetupFileName, targetSetupFileName }.Dump("Renaming Setup.exe");
	File.Copy(sourceSetupFileName, targetSetupFileName);	
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