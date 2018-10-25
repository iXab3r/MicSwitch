<Query Kind="Program" />

void Main()
{
	var appExeName = "MicSwitch.exe";
	var appName = Path.GetFileNameWithoutExtension(appExeName);
	var scriptDir = Path.GetDirectoryName(Util.CurrentQueryPath);
	var homeDir = Path.Combine(scriptDir, "Sources");

	var nuspecFileName = $"{appName}.nuspec";
	var binariesDir = @"bin\";
	var nuspecFilePath = Path.Combine(homeDir, nuspecFileName);
	var exeFilePath = Path.Combine(homeDir, binariesDir, appExeName);

	new { appExeName, appName, scriptDir, homeDir, nuspecFileName, binariesDir, nuspecFilePath }.Dump("Arguments");

	new[] { exeFilePath }.Dump("Reading version from .exe file...");

	var versionInfo = FileVersionInfo.GetVersionInfo(exeFilePath);
	var version = $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}";

	version.Dump("Version");

	var extensionsToExclude = new[] {
		".xml",
		".nupkg"
	};
	
	var binariesPath = Path.Combine(homeDir, binariesDir);

	new[] { binariesPath, nuspecFilePath }.Dump("Args");

	var files = Directory.GetFiles(binariesPath, "*.*", SearchOption.AllDirectories);
	files = files.Select(x => new{
			Relative = MakeRelativePath(binariesPath, x),
			Absolute = x
		})
		.Select(x => x.Relative)
		.ToArray();
	files = files.Where(x => !extensionsToExclude.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)).ToArray();
	files.Dump("Nuspec content");

	nuspecFilePath.Dump("Opening nuspec file...");
	var nuspecDocument = XElement.Load(nuspecFilePath);
	var ns = nuspecDocument.GetDefaultNamespace();
	
	var versionNode = nuspecDocument.Descendants(ns + "metadata").Single().Descendants(ns + "version").Single();
	versionNode.Dump("[BEFORE] Nuspec version");

	var filesNode = nuspecDocument.Descendants(ns + "files").SingleOrDefault();
	if (filesNode == null){
		filesNode = new XElement(ns + "files");
		nuspecDocument.Add(filesNode);
	}
	filesNode.Dump("[BEFORE] nuspec files list");
	filesNode.RemoveAll();
	foreach (var file in files)
	{
		var newElement = new XElement(ns+"file");
		newElement.SetAttributeValue("src", Path.Combine(binariesDir, file));
		newElement.SetAttributeValue("target", Path.Combine(@"lib\.net45", file));
		newElement.Attributes("xmlns").Remove();

		filesNode.Add(newElement);
	}
	filesNode.Dump("[AFTER] nuspec files list");
	versionNode.Value = version;
	versionNode.Dump("[AFTER] Nuspec version");

	File.Copy(nuspecFilePath, nuspecFilePath+".bak", true);
 	nuspecDocument.Save(nuspecFilePath);
}

// Define other methods and classes here
public static String MakeRelativePath(String fromPath, String toPath)
{
	if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
	if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

	Uri fromUri = new Uri(fromPath);
	Uri toUri = new Uri(toPath);

	if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

	Uri relativeUri = fromUri.MakeRelativeUri(toUri);
	String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

	if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
	{
		relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
	}

	return relativePath;
}