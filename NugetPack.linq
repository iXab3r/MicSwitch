<Query Kind="Program" />

void Main()
{
	var appExeName = "MicSwitch.exe";
	var appName = Path.GetFileNameWithoutExtension(appExeName);
	
	var scriptDir = Path.GetDirectoryName(Util.CurrentQueryPath);
	var homeDir = Path.Combine(scriptDir, "Sources");
	var toolsDir = Path.Combine(scriptDir, "Tools");
	var nugetPath = Path.Combine(toolsDir, "nuget.exe");
	
	var nuspecFileName = $"{appName}.nuspec";
	var nuspecFilePath = Path.Combine(homeDir, nuspecFileName);
	var version = GetSpecVersion(nuspecFilePath);
	version.Dump("NuSpec version");

	var nupkgFileName = $@"{appName}.{version}.nupkg";
	var nupkgFilePath = Path.Combine(scriptDir, nupkgFileName);

	Util.Cmd(nugetPath, $"pack {nuspecFilePath} -OutputDirectory \"{scriptDir}\" -Properties Configuration=Release", false);
}

private static string GetSpecVersion(string nuspecFilePath)
{
	new[] { nuspecFilePath }.Dump("Reading version from .nuspec file...");
	var nuspecDocument = XElement.Load(nuspecFilePath);
	var ns = nuspecDocument.GetDefaultNamespace();

	var version = nuspecDocument.Descendants(ns + "metadata").Single().Descendants(ns + "version").Single().Value;

	return version;
}