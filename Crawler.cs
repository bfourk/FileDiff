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
			Output.Files.Add(CrawledFile.Substring(CrawledFile.IndexOf("/./")+3));
		foreach (string Dir in Directories)
		{
			// Crawl subdirectory
			Output.Directories.Add(Dir.Substring(Dir.IndexOf("/./")+3));
			Crawl(NewPath, Dir.Split("/").Last(), Output);
		}
	}

	public static void FindFolderChanges(List<string> DirectoryListMain, List<string> DirectoryListSync, string MainDir, string SyncDir, ref List<string> Additions, ref List<string> Deletions)
	{
		foreach (string FolderLocation in DirectoryListMain)
			if (!DirectoryListSync.Contains(FolderLocation))
			{
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("+ [{0}]", FolderLocation);
				Additions.Add(FolderLocation);
			}

		foreach (string FolderLocation in DirectoryListSync)
			if (!DirectoryListMain.Contains(FolderLocation) && !FolderLocation.Contains(".DiffTrash"))
				Deletions.Add(FolderLocation);
		// Remove unnecessary folders (parent folders removed)
		Util.RecursiveRemove(Deletions, Deletions);
		Console.ForegroundColor = ConsoleColor.DarkRed;	
		foreach (string del in Deletions)
			Console.WriteLine("- [{0}]",del);
	}
	// Goes through all files in a list, checks if they were added, checks if different from sync folder.
	public static void FindFileChanges(string[] Main, CrawlInfo Sync, string MainDir, string SyncDir, ref List<string> Additions, ref List<string> Deletions, ref List<string> Changes)
	{
		if (Sync.Files == null || Sync.Directories == null)
			return;
		foreach (string FileLocation in Main)
		{
			// If the second directory contains file "FileLocation"
			if (!Sync.Files.Contains(FileLocation))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("+ {0}",FileLocation);
				Additions.Add(FileLocation);
			}
			else
			{
				// Hash the two files, check if different

				string File1Path = Path.Join(MainDir, FileLocation);
				string File2Path = Path.Join(SyncDir, FileLocation);
				try
				{
					if (!Util.CompareFiles(File1Path, File2Path))
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine("* {0}", FileLocation);
						Changes.Add(FileLocation);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("WARN: Failed to read file {0}\nStacktrace: {1}", File1Path, ex.ToString());
					continue;
				}
			}
			/* TODO: re-implement with care for multithreaded support
			// Calculate percentage, print to console

			string PercentOutput = "";
			int PercentNumber = (int)Math.Floor(((float)DirectoryIndex/MainDirectoryCnt)*100);
			if (PercentNumber < 10)
				PercentOutput = "{0}%\b\b";
			if (PercentNumber < 100)
				PercentOutput = "{0}%\b\b\b";
			if (PercentNumber == 100)
				PercentOutput = "{0}%\b\b\b\b";
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write(string.Format(PercentOutput, PercentNumber));

			DirectoryIndex++;
			*/
		}
	}
}