using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FileDiff;

internal class CrawlInfo
{
	public List<string>? Files { get; set; }
	public List<string>? Directories { get; set; }
}

internal static class Crawler
{
	public static void Crawl(string Root, string NewDir, CrawlInfo Output)
	{
		if (Output.Files == null || Output.Directories == null)
			return;

		string NewPath = Path.Join(Root,NewDir);

		string[] Files = Directory.GetFiles(NewPath);
		string[] Directories = Directory.GetDirectories(NewPath);

		foreach (string CrawledFile in Files) // Add file to list
			Output.Files.Add(CrawledFile.Substring(CrawledFile.IndexOf("/./") + 3));

		foreach (string Dir in Directories)
		{
			// Crawl subdirectory
			Output.Directories.Add(Dir.Substring(Dir.IndexOf("/./") + 3));
			Crawl(NewPath, Dir.Split("/").Last(), Output);
		}
	}

	public static void FindFolderChanges(SyncState State)
	{
		foreach (string FolderLocation in State.MainDirectoryDirs)
			if (!State.SyncDirectoryDirs.Contains(FolderLocation))
			{
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("+ [{0}]", FolderLocation);
				State.DirAdditions.Add(FolderLocation);
			}

		foreach (string FolderLocation in State.SyncDirectoryDirs)
			if (!State.MainDirectoryDirs.Contains(FolderLocation) && !FolderLocation.Contains(".DiffTrash"))
				State.DirDeletions.Add(FolderLocation);

		// Remove unnecessary folders (parent folders removed)
		Util.RecursiveRemove(State.DirDeletions, State.DirDeletions);

		Console.ForegroundColor = ConsoleColor.DarkRed;	
		foreach (string del in State.DirDeletions)
			Console.WriteLine("- [{0}]",del);
	}

	// Goes through all files in a list, checks if they were added, checks if different from sync folder.
	public static void FindFileChanges(string[] Main, SyncState State)
	{
		if (State.SyncDirectoryFiles == null || State.SyncDirectoryDirs == null)
			return;

		foreach (string FileLocation in Main)
		{
			// If the second directory contains file "FileLocation"
			if (!State.SyncDirectoryFiles.Contains(FileLocation))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("+ {0}", FileLocation);
				State.FileAdditions.Add(FileLocation);
			}
			else
			{
				// Hash the two files, check if different

				string File1Path = Path.Join(State.MainDirectory, FileLocation);
				string File2Path = Path.Join(State.SyncDirectory, FileLocation);

				try
				{
					if (!Util.CompareFiles(File1Path, File2Path))
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine("* {0}", FileLocation);
						State.FileChanges.Add(FileLocation);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("WARN: Failed to read file {0}\nStacktrace: {1}", File1Path, ex.ToString());
					continue;
				}
			}
		}
	}
}