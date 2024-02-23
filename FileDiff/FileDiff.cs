using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace FileDiff;

public class FDiff
{
	private const int Threads = 6; // Can change to whatever you want
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
		if (!Util.RequestYN(string.Format("\nDirectory 1: \"{0}\"\nDirectory 2: \"{1}\"\nIs this correct?", MainDirectory, SyncDirectory)))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}
		sw.Start();
		// Build two directory lists of MainDirectory and SyncDirectory
		CrawlInfo MainDirectoryList = new CrawlInfo
		{
			Files = new List<string>(),
			Directories = new List<string>()
		};
		CrawlInfo SyncDirectoryList = new CrawlInfo
		{
			Files = new List<string>(),
			Directories = new List<string>()
		};
		//List<string> MainDirectoryList = new List<string>();
		//List<string> SyncDirectoryList = new List<string>();

		Console.WriteLine("Building Directory List 1");
		Crawler.Crawl(MainDirectory,".",MainDirectoryList);
		Console.WriteLine("Building Directory List 2");
		Crawler.Crawl(SyncDirectory,".",SyncDirectoryList);

		//int MainDirectoryCnt = MainDirectoryList.Count; // For the percentage
		Console.WriteLine("Completed:\nMain Directory File Count: {0}\nTarget Directory File Count: {1}\n",
			MainDirectoryList.Files.Count,
			SyncDirectoryList.Files.Count
		);
		Console.Write("Main Directory Directory Count: {0}\nTarget Directory Directory Count: {1}\n",
			MainDirectoryList.Directories.Count,
			SyncDirectoryList.Directories.Count
		);

		//int DirectoryIndex = 0;

		// Directory Lists
		List<string> dAdd = new List<string>();
		List<string> dDel = new List<string>();

		// File Lists
		List<string> fAdd = new List<string>();
		List<string> fDel = new List<string>();
		List<string> fChanges = new List<string>();

		List<string[]> FileLists = new List<string[]>(); // For multithreaded support

		// Determine how many files to give each thread, make a string[] array with the filenames.
		int FilesPerList = (int)Math.Floor((double)MainDirectoryList.Files.Count / Threads);
		int Remainder = MainDirectoryList.Files.Count - (FilesPerList * Threads);
		
		for (int i = 0; i < Threads; i++)
		{
			string[] FilePool = new string[FilesPerList];
			for (int f = 0; f < FilesPerList; f++)
				FilePool[f] = MainDirectoryList.Files[i*FilesPerList+f];
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
					RemainderAppended[FilesPerList+f] = MainDirectoryList.Files[FilesPerList*Threads+f];
				FileLists[0] = RemainderAppended;
			}

		// Directory changes are single-threaded because the performance impact is very small
		// Directories also can't "change" making it much simpler needing to only detect additions/deletions.
		Crawler.FindFolderChanges(MainDirectoryList.Directories, SyncDirectoryList.Directories, MainDirectory, SyncDirectory, ref dAdd, ref dDel);
		
		// Start all threads
		int TotalFinished = Threads;
		for (int i = 0; i < Threads; i++)
		{
			new Thread(()=>{
				int CurrentList = i;
				// Search for additions/changes
				Crawler.FindFileChanges(FileLists[i], SyncDirectoryList, MainDirectory, SyncDirectory, ref fAdd, ref fDel, ref fChanges);
				TotalFinished--;
			}).Start();
			Thread.Sleep(1);
		}

		// Wait for all threads to finish
		while (TotalFinished != 0)
			Thread.Sleep(10);

		// Search for deletions
		foreach(string FileLocation in SyncDirectoryList.Files)
			if (!MainDirectoryList.Files.Contains(FileLocation))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("- {0}",FileLocation);
					fDel.Add(FileLocation);
				}

		Console.ForegroundColor = ConsoleColor.White;
		int Total = fAdd.Count() + fDel.Count() + fChanges.Count() + dAdd.Count() + dDel.Count();

		if (Total == 0)
			Console.WriteLine("No changes!");
		else
			Console.WriteLine("{0} File Addition(s)\n{1} File Deletion(s)\n{2} File Modification(s)\n{3} Directory Addition(s)\n{4} Directory Deletion(s)\n\n\t{5} Total",
				fAdd.Count(), fDel.Count(), fChanges.Count(), dAdd.Count(), dDel.Count(), Total
			);
		sw.Stop();
		Console.WriteLine("Finished in {0}",Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);
		if (Total == 0)
			return;
		
	}
}