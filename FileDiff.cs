using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using Cache;

namespace FileDiff;

internal class SyncState
{
	public required string MainDirectory {get; set;}
	public required string SyncDirectory {get; set;}

	public string[] IgnoreList {get; set;} = new string[0];

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

	public DirCache? MainDirCache {get; set;}
	public DirCache? SyncDirCache {get; set;}
}

public class FDiff
{
	private static int Threads = Environment.ProcessorCount;

	private static string? MainDirectory, SyncDirectory;
	private static Stopwatch sw = new Stopwatch(); // For calculating the total time
	private static ArgParse? arg;

	private static bool DoGarbage = true;

	// Other classes need to access these
	public static bool DoCache = true;
	public static bool CacheOnly = false;

	private static void CacheToDisk(SyncState State)
	{
		Console.WriteLine("Save Cache to Disk");
		try
		{
			byte[]? Main = State.MainDirCache!.Serialize();
			byte[]? Sync = State.MainDirCache!.Serialize();
			if (Main == null || Sync == null)
			{
				Console.WriteLine("Failed to serialize caches");
			} else
			{
				File.WriteAllBytes(Path.Join(MainDirectory, ".fdc"), Main);
				File.WriteAllBytes(Path.Join(SyncDirectory, ".fdc"), Sync);
			}
		}
		catch
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Failed to write caches to disk");
			Console.ForegroundColor = ConsoleColor.White;
		}
	}

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
			Console.WriteLine("-c --cache-only\t\tGenerate cache files without syncing");
			Console.WriteLine("-nc --no-cache\t\tDo not use or generate cache files");
			Environment.Exit(0);
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
		DoCache = (arg.GetArg("-nc", "--no-cache") == null);
		CacheOnly = !(arg.GetArg("-c", "--cache-only") == null);

		if (!DoCache && CacheOnly)
		{
			Console.WriteLine("No Cache and Cache Only must not be set together");
			Environment.Exit(1);
		}

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


		// Build two directory lists of MainDirectory and SyncDirectory
		{
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

			string MainCache = "";
			string SyncCache = "";

			if (DoCache)
			{
				string MainCachePath = Path.Join(MainDirectory, ".fdc");
				string SyncCachePath = Path.Join(MainDirectory, ".fdc");

				if (File.Exists(MainCachePath))
					MainCache = MainCachePath;
				if (File.Exists(SyncCachePath))
					SyncCache = SyncCachePath;
			}

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
				FileChanges = new List<string>(),

				MainDirCache = DoCache ? new DirCache(MainDirectory, MainCache == "" ? null : MainCache) : null,
				SyncDirCache = DoCache ? new DirCache(SyncDirectory, SyncCache == "" ? null : SyncCache) : null
			};
		}

		// Check for and parse ignore lists
		{
			string[]? MainDirIgnore = null;
			string[]? SyncDirIgnore = null;

			string IgnorePathMain = Path.Join(MainDirectory, ".fdignore");
			string IgnorePathSync = Path.Join(SyncDirectory, ".fdignore");

			if (File.Exists(IgnorePathMain))
				try
				{
					MainDirIgnore = File.ReadAllLines(IgnorePathMain);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Failed to open .fdignore MAIN: {0}", ex);
				}
			if (File.Exists(IgnorePathSync))
				try
				{
					SyncDirIgnore = File.ReadAllLines(IgnorePathSync);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Failed to open .fdignore SYNC: {0}", ex);
				}

			if (MainDirIgnore != null && SyncDirIgnore != null)
			{
				if (Util.RequestYN(".fdignore file exists in both main and sync. Do you want to merge the contents?"))
				{
					string[] Final = new string[MainDirIgnore.Length + SyncDirIgnore.Length];
					for (int i = 0; i < MainDirIgnore.Length; i++)
						Final[i] = MainDirIgnore[i];
					for (int i = 0; i < SyncDirIgnore.Length; i++)
						Final[i + MainDirIgnore.Length] = SyncDirIgnore[i];

					State.IgnoreList = Final;	
				}
			}
			else
			{
				if (MainDirIgnore != null)
					State.IgnoreList = MainDirIgnore;
				if (SyncDirIgnore != null)
					State.IgnoreList = SyncDirIgnore;
			}
		}

//		Environment.Exit(1);

		Console.WriteLine("Completed:\nMain File Count: {0}\nTarget File Count: {1}",
			State.MainDirectoryFiles.Count,
			State.SyncDirectoryFiles.Count
		);
		Console.WriteLine("Main Directory Count: {0}\nTarget Directory Count: {1}\n",
			State.MainDirectoryDirs.Count,
			State.SyncDirectoryDirs.Count
		);

		if (CacheOnly)
		{
			Console.WriteLine("Generating/Updating Directory1 Cachefile");
			for (int i = 0; i < State.MainDirectoryFiles.Count; i++)
			{
				Console.Write("\rCurrently Processing {0}/{1} in Directory1", i + 1, State.MainDirectoryFiles.Count);
				string MainFile = State.MainDirectoryFiles[i];

				// Skip special files
				string Initial = MainFile.Split('/')[0];
				if (Initial == ".fdc" || Initial == ".fdignore" || Initial == ".DiffTrash")
					continue;

				Node? CheckNode = State.MainDirCache!.ReadCache(MainFile);
				if (CheckNode == null)
				{
					State.MainDirCache!.AddCache(MainFile);
					continue;
				}
				State.MainDirCache!.UpdCache(MainFile);
			}
			Console.WriteLine("\nGenerating/Updating Directory2 Cachefile");
			for (int i = 0; i < State.SyncDirectoryFiles.Count; i++)
			{
				Console.Write("\rCurrently Processing {0}/{1} in Directory2", i + 1, State.SyncDirectoryFiles.Count);
				string SyncFile = State.SyncDirectoryFiles[i];

				// Skip special files
				string Initial = SyncFile.Split('/')[0];
				if (Initial == ".fdc" || Initial == ".fdignore" || Initial == ".DiffTrash")
					continue;

				Node? CheckNode = State.SyncDirCache!.ReadCache(SyncFile);
				if (CheckNode == null)
				{
					State.SyncDirCache.AddCache(SyncFile);
					continue;
				}
				State.MainDirCache!.UpdCache(SyncFile);
			}
			Console.WriteLine();
			CacheToDisk(State);
//			State.MainDirCache.DbgPrint();
			Console.WriteLine("Cache Update Completed");
			Environment.Exit(0);
		}

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
			if (!State.MainDirectoryFiles.Contains(FileLocation)
				&& !FileLocation.Contains(".DiffTrash")
				&& !FileLocation.Contains(".fdc")
				&& !FileLocation.Contains(".fdignore"))
					State.FileDeletions.Add(FileLocation);

		// Remove unnecessary files (parent folders removed)
		Util.IterativeRemove(State.DirDeletions, State.FileDeletions);

		Console.ForegroundColor = ConsoleColor.Red;
		foreach (string del in State.FileDeletions)
			Console.WriteLine("- {0}",del);
		Console.ForegroundColor = ConsoleColor.White;

		int Total = State.FileAdditions.Count + State.FileDeletions.Count + State.FileChanges.Count + State.DirAdditions.Count + State.DirDeletions.Count;

/* // This shouldn't be necessary as we already delete objects from cache, leaving here just in case
		Console.WriteLine("Final Cache Prune");
		Util.PruneCache(State.MainDirectory, State.MainDirCache);
		Util.PruneCache(State.SyncDirectory, State.SyncDirCache);
*/
		if (DoCache && Total != 0) // If there are zero changes, there's no need to rewrite the cache file
			CacheToDisk(State);

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
		Console.WriteLine("Finished in {0}", Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);

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