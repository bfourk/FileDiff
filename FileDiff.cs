using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace FileDiff;

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

		Console.WriteLine("Completed:\nMain File Count: {0}\nTarget File Count: {1}",
			MainDirectoryList.Files.Count,
			SyncDirectoryList.Files.Count
		);
		Console.WriteLine("Main Directory Count: {0}\nTarget Directory Count: {1}\n",
			MainDirectoryList.Directories.Count,
			SyncDirectoryList.Directories.Count
		);

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
				string[] RemainderAppended = new string[FilesPerList + Remainder];
				for (int f = 0; f < FilesPerList; f++)
					RemainderAppended[f] = FileLists[0][f];
				for (int f = 0; f < Remainder; f++)
					RemainderAppended[FilesPerList+f] = MainDirectoryList.Files[FilesPerList * Threads + f];
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
				Crawler.FindFileChanges(FileLists[i], SyncDirectoryList, MainDirectory, SyncDirectory, fAdd, fDel, fChanges);
				TotalFinished--;
			}).Start();
			Thread.Sleep(1);
		}

		// Wait for all threads to finish
		while (TotalFinished != 0)
			Thread.Sleep(100);

		// Search for deletions
		foreach(string FileLocation in SyncDirectoryList.Files)
			if (!MainDirectoryList.Files.Contains(FileLocation) && !FileLocation.Contains(".DiffTrash"))
					fDel.Add(FileLocation);

		// Remove unnecessary files (parent folders removed)
		Util.RecursiveRemove(dDel, fDel);

		Console.ForegroundColor = ConsoleColor.Red;
		foreach (string del in fDel)
			Console.WriteLine("- {0}",del);
		Console.ForegroundColor = ConsoleColor.White;

		int Total = fAdd.Count() + fDel.Count() + fChanges.Count() + dAdd.Count() + dDel.Count();
		if (Total == 0)
			Console.WriteLine("No changes!");
		else
		{
			Console.WriteLine("{0} File Addition(s)", fAdd.Count());
			Console.WriteLine("{0} File Deletion(s)", fDel.Count());
			Console.WriteLine("{0} File Modification(s)", fChanges.Count());
			Console.WriteLine("{0} Directory Addition(s)", dAdd.Count());
			Console.WriteLine("{0} Directory Deletion(s)", dDel.Count());
			Console.WriteLine("{0} Total", Total);
		}

		sw.Stop();
		Console.WriteLine("Finished in {0}",Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);

		if (Total == 0)
			return;

		if (!Util.RequestYN("Would you like to synchronize these directories?"))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}
		if (!Util.RequestYN("This will ADD, DELETE, or CHANGE files in the second directory. Are you sure?"))
		{
			Console.WriteLine("Exiting");
			Environment.Exit(0);
			return;
		}

		if ((fAdd.Count() + dAdd.Count()) == 0)
			Console.WriteLine("No Additions, Skipping");
		else
			if (Util.RequestYN("Synchronize Additions?"))
			{
				foreach (string add in dAdd)
				{
					string DirPath = Path.Join(SyncDirectory, add);
					Console.WriteLine("+ [{0}]", add);
					try
					{
						Directory.CreateDirectory(DirPath);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to create directory {0}\nReason: {1}", add, ex.ToString());
					}
				}
				foreach (string add in fAdd)
				{
					string Path1 = Path.Join(MainDirectory, add);
					string Path2 = Path.Join(SyncDirectory, add);
					Console.WriteLine("+ {0}", add);
					try
					{
						File.Copy(Path1, Path2);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to copy file {0}\nReason: {1}", add, ex.ToString());
						continue;
					}
				}
			}

		if (fChanges.Count() == 0)
			Console.WriteLine("No Changes, Skipping");
		else
			if (Util.RequestYN("Synchronize Changes?"))
				foreach (string ch in fChanges)
				{
					string Path1 = Path.Join(MainDirectory, ch);
					string Path2 = Path.Join(SyncDirectory, ch);
					Console.WriteLine("* {0}", ch);
					try
					{
						using (FileStream writer = new FileStream(Path2, FileMode.Create))
							using (FileStream reader = new FileStream(Path1, FileMode.Open))
							{
								byte[] buffer = new byte[8192];
								while (true)
								{
									int read = reader.Read(buffer, 0, 8192);
									if (read <= 0)
										break;
									writer.Write(buffer, 0, read);
								}
							}
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to change file {0}\nReason: {1}", ch, ex.ToString());
						continue;
					}
				}

		int DuplicateInc = 0;
		if ((dDel.Count() + fDel.Count()) == 0)
			Console.WriteLine("No Deletions, Skipping");
		else
			if (Util.RequestYN("Synchronize Deletions?"))
			{
				string GarbagePath = Path.Join(SyncDirectory,".DiffTrash");
				if (!Directory.Exists(GarbagePath) && DoGarbage)
					Directory.CreateDirectory(GarbagePath);

				// Files
				foreach (string del in fDel)
				{
					if (DoGarbage)
					{
						DuplicateInc++;
						string Path1 = Path.Join(SyncDirectory, del);
						// The file could have been deleted at this point, double-check
						if (!File.Exists(Path1))
							continue;
						Console.WriteLine("- {0}", del);
						try
						{
							string NewPath = Path.Join(GarbagePath, del);

							// Re-create directory path in trash folder
							Util.RecreateDirectoryTree(GarbagePath, NewPath);
							if (File.Exists(NewPath))
							{
								Console.WriteLine("Warn: File with similar name already exists in trash, adding number to beginning");
								File.Move(Path1, Path.Join(GarbagePath,string.Format("{0}-{1}", DuplicateInc.ToString(), del)));
							}
							else
								File.Move(Path1, NewPath);
						}
						catch (Exception ex)
						{
							Console.WriteLine("Failed to trash file {0}\nReason: {1}", del, ex.ToString());
							continue;
						}
						continue;
					}
					// Garbage is disabled, just delete it
					try
					{
						File.Delete(Path.Join(SyncDirectory, del));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to delete file {0}\nReason: {1}", del, ex.ToString());
					}
				}
				// Folders
				foreach (string del in dDel)
				{
					if (DoGarbage)
					{
						try
						{
							string DirPath = Path.Join(SyncDirectory, del);
							string NewPath = Path.Join(GarbagePath, del);
							Util.RecreateDirectoryTree(GarbagePath, NewPath);
							if (Directory.Exists(NewPath))
							{
								Console.WriteLine("Warn: Folder with similar name already exists in trash, adding number to beginning");
								Directory.Move(DirPath, Path.Join(GarbagePath, string.Format("{0}-{1}", DuplicateInc.ToString(), del)));
							}
							else
								Directory.Move(DirPath, NewPath);
							Console.WriteLine("- {0}", del);
						}
						catch (Exception ex)
						{
							Console.WriteLine("Failed to trash directory {0}\nReason: {1}", del, ex.ToString());
						}
						continue;
					}
					// Garbage is disabled, just delete it
					try
					{
						Directory.Delete(Path.Join(SyncDirectory, del), true);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to delete directory {0}\nReason: {1}", del, ex.ToString());
					}
				}
			}
		Console.WriteLine("Finished");
	}
}