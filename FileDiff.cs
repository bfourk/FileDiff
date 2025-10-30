using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace FileDiff;

internal class SyncState
{
	public required string MainDirectory {get; set;}
	public required string SyncDirectory {get; set;}

	public required List<string> MainDirectoryFiles {get; set;}
	public required List<string> MainDirectoryDirs {get; set;}

	public required List<string> SyncDirectoryFiles {get; set;}
	public required List<string> SyncDirectoryDirs {get; set;}

	public required List<string> DirAdditions {get; set;}
	public required List<string> DirDeletions {get; set;}

	// File Lists
	public required List<string> FileAdditions {get; set;}
	public required List<string> FileDeletions {get; set;}
	public required List<string> FileChanges {get; set;}
}

public class FDiff
{
	private static int Threads = Environment.ProcessorCount;

	private static string? MainDirectory, SyncDirectory;
	private static Stopwatch sw = new Stopwatch(); // For calculating the total time
	private static ArgParse? arg;

	private static bool DoGarbage = true;

	public static void Main(string[] args)
	{
		arg = new ArgParse(args);

		if (arg.GetArg("-h", "--help") != null)
		{
			Console.WriteLine("-nt --no-trash\t\tDelete removed files instead of creating a trash directory");
			Console.WriteLine("-d1 --main-directory\tThe main directory to check");
			Console.WriteLine("-d2 --sync-directory\tThe directory to sync files to");
			Console.WriteLine("-y --yes\t\tAlways assume yes to asked questions");
			Console.WriteLine("-t --threads\t\tHow many threads to use. Defaults to your system's core count");
			return;
		}

		string? UserThread = arg.GetArg("-t", "--threads", "--thread");
		if (UserThread != null && !int.TryParse(UserThread, out Threads))
			Console.WriteLine("Could not parse input thread count. Defaulting to {0}", Threads);

		if (Threads < 1)
			Threads = 1;

		Console.WriteLine("FileDiff with {0} Thread{1}", Threads, Threads == 1 ? "" : "s");

		// Make RequestYN return true without asking if -y argument is supplied
		Util.AlwaysYes = (arg.GetArg("-y", "--yes") != null);
		DoGarbage = (arg.GetArg("-nt", "--no-trash") == null);

		MainDirectory = arg.GetArg("-d1", "--main-directory");
		if (MainDirectory == null)
		{
			Console.Write("Input Directory 1: ");
			MainDirectory = Console.ReadLine();
		}

		SyncDirectory = arg.GetArg("-d2", "--sync-directory");
		if (SyncDirectory == null)
		{
			Console.Write("\nInput Directory 2: ");
			SyncDirectory = Console.ReadLine();
		}

		if (MainDirectory == null || SyncDirectory == null)
			return;

		// Confirm with user if both directories are the correct ones
		if (!Util.RequestYN(string.Format("\nDirectory 1: \"{0}\"\nDirectory 2: \"{1}\"\nIs this correct?", MainDirectory, SyncDirectory)))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}

		sw.Start();


		SyncState? State;
		{
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

			Console.WriteLine("Building Directory List 1");
			Crawler.Crawl(MainDirectory,".",MainDirectoryList);
			Console.WriteLine("Building Directory List 2");
			Crawler.Crawl(SyncDirectory,".",SyncDirectoryList);

			State = new SyncState
			{
				MainDirectory = MainDirectory,
				SyncDirectory = SyncDirectory,

				MainDirectoryFiles = MainDirectoryList.Files,
				MainDirectoryDirs = MainDirectoryList.Directories,

				SyncDirectoryFiles = SyncDirectoryList.Files,
				SyncDirectoryDirs = SyncDirectoryList.Directories,

				DirAdditions = new List<string>(),
				DirDeletions = new List<string>(),

				FileAdditions = new List<string>(),
				FileDeletions = new List<string>(),
				FileChanges = new List<string>()
			};
		}

		Console.WriteLine("Completed:\nMain File Count: {0}\nTarget File Count: {1}",
			State.MainDirectoryFiles.Count,
			State.SyncDirectoryFiles.Count
		);
		Console.WriteLine("Main Directory Count: {0}\nTarget Directory Count: {1}\n",
			State.MainDirectoryDirs.Count,
			State.SyncDirectoryDirs.Count
		);

		List<string[]> FileLists = new List<string[]>(); // For multithreaded support

		// Determine how many files to give each thread, make a string[] array with the filenames.
		int FilesPerList = (int)Math.Floor((double)State.MainDirectoryFiles.Count / Threads);
		int Remainder = State.MainDirectoryFiles.Count - (FilesPerList * Threads);
		
		for (int i = 0; i < Threads; i++)
		{
			string[] FilePool = new string[FilesPerList];
			for (int f = 0; f < FilesPerList; f++)
				FilePool[f] = State.MainDirectoryFiles[i*FilesPerList+f];
			FileLists.Add(FilePool);
		}

		// If there's extra files, add to the first list.
		if (Remainder != 0)
			for (int i = 0; i < Remainder; i++)
			{
				string[] RemainderAppended = new string[FilesPerList + Remainder];
				for (int f = 0; f < FilesPerList; f++)
					RemainderAppended[f] = FileLists[0][f];
				for (int f = 0; f < Remainder; f++)
					RemainderAppended[FilesPerList+f] = State.MainDirectoryFiles[FilesPerList * Threads + f];
				FileLists[0] = RemainderAppended;
			}

		// Directory changes are single-threaded because the performance impact is very small
		// Directories also can't "change" making it much simpler as we only need to detect additions/deletions.
		Crawler.FindFolderChanges(State);
		
		// Start all threads
		int TotalFinished = Threads;
		for (int i = 0; i < Threads; i++)
		{
			new Thread(() => {
				int CurrentList = i;
				// Search for additions/changes
				Crawler.FindFileChanges(FileLists[i], State);
				TotalFinished--;
			}).Start();
			Thread.Sleep(1);
		}

		// Wait for all threads to finish
		while (TotalFinished != 0)
			Thread.Sleep(100);

		// Search for deletions
		foreach(string FileLocation in State.SyncDirectoryFiles)
			if (!State.MainDirectoryFiles.Contains(FileLocation) && !FileLocation.Contains(".DiffTrash"))
					State.FileDeletions.Add(FileLocation);

		// Remove unnecessary files (parent folders removed)
		Util.RecursiveRemove(State.DirDeletions, State.FileDeletions);

		Console.ForegroundColor = ConsoleColor.Red;
		foreach (string del in State.FileDeletions)
			Console.WriteLine("- {0}",del);
		Console.ForegroundColor = ConsoleColor.White;

		int Total = State.FileAdditions.Count + State.FileDeletions.Count + State.FileChanges.Count + State.DirAdditions.Count + State.DirDeletions.Count;
		if (Total == 0)
			Console.WriteLine("No changes!");
		else
		{
			Console.WriteLine("{0} File Addition(s)", State.FileAdditions.Count);
			Console.WriteLine("{0} File Deletion(s)", State.FileDeletions.Count);
			Console.WriteLine("{0} File Modification(s)", State.FileChanges.Count);
			Console.WriteLine("{0} Directory Addition(s)", State.DirAdditions.Count);
			Console.WriteLine("{0} Directory Deletion(s)", State.DirDeletions.Count);
			Console.WriteLine("{0} Total", Total);
		}

		sw.Stop();
		Console.WriteLine("Finished in {0}",Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);

		if (Total == 0)
			return;

		if (!Util.RequestYN("Would you like to synchronize these directories? This will ADD, DELETE, or CHANGE files in the second directory."))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}

		Synchronizer.Sync(State, DoGarbage);
		Console.WriteLine("Finished");
	}
}