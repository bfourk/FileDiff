namespace FileDiff;

internal static class Crawler
{
	private static void Crawl(string Root, string NewDir, List<string> Output)
	{
		string NewPath = Path.Join(Root,NewDir);
		string[] Files = Directory.GetFiles(NewPath);
		string[] Directories = Directory.GetDirectories(NewPath);
		foreach (string File in Files)
			// Add file to the list
			Output.Add(File.Substring(File.IndexOf("/./")+3));
		foreach (string Dir in Directories)
			// Crawl subdirectory
			Crawl(NewPath, Dir.Split("/").Last(), Output);
	}

	// Goes through all files in a list, checks if they were added, checks if different from sync folder.
	private static void SearchListForChanges(string[] Main, List<string> Sync, string MainDir, string SyncDir, ref List<string> Additions, ref List<string> Deletions, ref List<string> Changes)
	{
		foreach(string FileLocation in Main)
		{
			// If the second directory contains file "FileLocation"
			if (!Sync.Contains(FileLocation))
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
					Console.WriteLine("WARN: Failed to read file {0}/{1}\nStacktrace: {2}",File1Path, File2Path, ex.ToString());
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