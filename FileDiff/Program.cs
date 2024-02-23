using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace FileDiff;

public class FDiff
{
	private const int Threads = 6; // Can change to whatever you want
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
	private static bool RequestYN(string arg)
	{
		while (true)
		{
			Console.Write("\n{0} [Y/n] [ ]\b\b", arg);
			char tChar = Convert.ToChar(Console.ReadKey().Key);
			Console.Write("\n");
			if (Char.ToLower(tChar) == 'n')
				return false;
			if (Char.ToLower(tChar) == 'y')
				return true;
		}
	}
	public static void Main(string[] args)
	{
		Stopwatch sw = new Stopwatch(); // For calculating the total time
		Console.Write("Input Directory 1: ");
		string? MainDirectory = "/tmp/ramdisk/tmp1";//Console.ReadLine();
		Console.Write("\nInput Directory 2: ");
		string? SyncDirectory = "/tmp/ramdisk/tmp2";//Console.ReadLine();
		if (MainDirectory == null || SyncDirectory == null)
		{
			Environment.Exit(1);
			return;
		}
		// Confirm with user if both directories are the correct ones
		if (!RequestYN(string.Format("\nDirectory 1: \"{0}\"\nDirectory 2: \"{1}\"\nIs this correct?", MainDirectory, SyncDirectory)))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}
		sw.Start();
		// Build two directory lists of MainDirectory and SyncDirectory
		List<string> MainDirectoryList = new List<string>();
		List<string> SyncDirectoryList = new List<string>();

		Console.WriteLine("Building Directory List 1");
		Crawl(MainDirectory,".",MainDirectoryList);
		Console.WriteLine("Building Directory List 2");
		Crawl(SyncDirectory,".",SyncDirectoryList);

		int MainDirectoryCnt = MainDirectoryList.Count; // For the percentage
		Console.WriteLine("Completed:\nMain Directory File Count: {0}\nTarget Directory File Count: {1}\n",MainDirectoryList.Count, SyncDirectoryList.Count);

		//int DirectoryIndex = 0;
		List<string> Additions = new List<string>();
		List<string> Deletions = new List<string>();
		List<string> Changes = new List<string>();

		List<string[]> FileLists = new List<string[]>(); // For multithreaded support

		// Determine how many files to give each thread, make a string[] array with the filenames.
		int FilesPerList = (int)Math.Floor((double)MainDirectoryList.Count / Threads);
		int Remainder = MainDirectoryList.Count - (FilesPerList * Threads);
		
		for (int i = 0; i < Threads; i++)
		{
			string[] FilePool = new string[FilesPerList];
			for (int f = 0; f < FilesPerList; f++)
				FilePool[f] = MainDirectoryList[i*FilesPerList+f];
			FileLists.Add(FilePool);
		}

		// If there's extra files, add to the first list.
		if (Remainder != 0)
			for (int i = 0; i < Remainder; i++)
			{
				string[] RemainderAppended = new string[FilesPerList+Remainder];
				for (int f = 0; f < FilesPerList; f++)
					RemainderAppended[f] = FileLists[0][f];
				for (int f = 0; f < Remainder; f++)
					RemainderAppended[FilesPerList+f] = MainDirectoryList[FilesPerList*Threads+f];
				FileLists[0] = RemainderAppended;
			}

		// Start all threads
		int TotalFinished = Threads;
		for (int i = 0; i < Threads; i++)
		{
			new Thread(()=>{
				int CurrentList = i;
				// Search for additions/changes
				SearchListForChanges(FileLists[i], SyncDirectoryList, MainDirectory, SyncDirectory, ref Additions, ref Deletions, ref Changes);
				TotalFinished--;
			}).Start();
			Thread.Sleep(1);
		}

		// Wait for all threads to finish
		while (TotalFinished != 0)
			Thread.Sleep(10);

		// Search for deletions
		foreach(string FileLocation in SyncDirectoryList)
			if (!MainDirectoryList.Contains(FileLocation))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("- {0}",FileLocation);
					Deletions.Add(FileLocation);
				}

		Console.ForegroundColor = ConsoleColor.White;
		int Total = Additions.Count() + Deletions.Count() + Changes.Count();

		if (Total == 0)
			Console.WriteLine("No changes!");
		else
			Console.WriteLine("{0} Addition(s)\n{1} Deletion(s)\n{2} Modification(s)\n\n\t{3} Total", Additions.Count(), Deletions.Count(), Changes.Count(), Total);
		sw.Stop();
		Console.WriteLine("Finished in {0}",Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);
		if (Total == 0)
			return;
		
	}
}