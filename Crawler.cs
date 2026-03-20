using System;
using System.IO;
using System.Collections.Generic;

using Cache;

namespace FileDiff;

internal class CrawlInfo
{
	public List<string>? Files { get; set; }
	public List<string>? Directories { get; set; }
}

internal static class Crawler
{
	private static bool DoCache => FDiff.DoCache;
	private static bool CheckCreateTime => FDiff.CheckCreateTime;

	// Internal Functions

	private static bool CheckIgnoreList(string FileLocation, string[] IgnoreList)
	{
			for (int i = 0; i < IgnoreList.Length; i++)
			{
				if (IgnoreList[i].Trim() == "")
					continue;
				if (FileLocation.StartsWith(IgnoreList[i]))
					return true;
			}

		return false;
	}

	private static bool HashesEqual(byte[] Hash1, byte[] Hash2)
	{
		if (Hash1.Length != Hash2.Length)
			return false;

		for (int i = 0; i < Hash1.Length; i++)
			if (Hash1[i] != Hash2[i])
				return false;

		return true;
	}

	// Public Functions

	public static void Crawl(string Root, string NewDir, CrawlInfo Output, FDiff.CrawlStatusDelegate Delegate)
	{
		if (Output.Files == null || Output.Directories == null)
			return;

		string NewPath = Path.Join(Root, NewDir);

		string[] Files = Directory.GetFiles(NewPath);
		string[] Directories = Directory.GetDirectories(NewPath);

		foreach (string CrawledFile in Files) // Add file to list
			Output.Files.Add(CrawledFile.Substring(CrawledFile.IndexOf("/./") + 3));

		Delegate(Output.Files.Count);

		foreach (string Dir in Directories)
		{
			// Crawl subdirectory
			Output.Directories.Add(Dir.Substring(Dir.IndexOf("/./") + 3));

			string[] DirSplit = Dir.Split('/');
			Crawl(NewPath, DirSplit[DirSplit.Length - 1], Output, Delegate);
		}
	}

	public static void FindFolderChanges(SyncState State)
	{
		foreach (string FolderLocation in State.MainDirectoryDirs)
		{
			if (CheckIgnoreList(FolderLocation, State.IgnoreList))
				continue;
			if (!State.SyncDirectoryDirs.Contains(FolderLocation))
			{
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("+ [{0}]", FolderLocation);
				State.DirAdditions.Add(FolderLocation);
			}
		}

		foreach (string FolderLocation in State.SyncDirectoryDirs)
		{
			if (CheckIgnoreList(FolderLocation, State.IgnoreList))
				continue;
			if (!State.MainDirectoryDirs.Contains(FolderLocation) && !FolderLocation.Contains(".DiffTrash"))
				State.DirDeletions.Add(FolderLocation);
		}

		// Remove unnecessary folders (parent folders removed)
		Util.IterativeRemove(State.DirDeletions, State.DirDeletions);

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
			{ // Skip cache file and ignore file
				string[] NameSplit = FileLocation.Split('.');
				string FileName = NameSplit[NameSplit.Length - 1];
				if (FileName == "fdc" || FileName == "fdignore")
					continue;
			}

			if (CheckIgnoreList(FileLocation, State.IgnoreList))
				continue;

			// If the second directory contains file "FileLocation"
			if (!State.SyncDirectoryFiles.Contains(FileLocation))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("+ {0}", FileLocation);
				State.FileAdditions.Add(FileLocation);
				continue;
			}
			// File exists in both directories, check if they're different

			string File1Path = Path.Join(State.MainDirectory, FileLocation);
			string File2Path = Path.Join(State.SyncDirectory, FileLocation);

			if (DoCache)
			{
				// Check cache first
				Node? MainNode = State.MainDirCache!.ReadCache(FileLocation);
				Node? SyncNode = State.SyncDirCache!.ReadCache(FileLocation);

				if (MainNode != null && SyncNode != null)
				{
					FileInfo? MainFile;
					FileInfo? SyncFile;

					try
					{
						MainFile = new FileInfo(File1Path);
						SyncFile = new FileInfo(File2Path);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to get file info: {0}", ex);
						continue;
					}

					// Check if file data conflicts with cache data

					// Check main node
					bool Check1 = (!CheckCreateTime ? true : (MainFile.CreationTime == MainNode.CreateDate) &&
							MainFile.LastWriteTime == MainNode.ModifiedDate &&
							MainFile.Length == MainNode.Size);

					// Check sync node
					bool Check2 = (!CheckCreateTime ? true : (SyncFile.CreationTime == SyncNode.CreateDate) &&
							SyncFile.LastWriteTime == SyncNode.ModifiedDate &&
							SyncFile.Length == SyncNode.Size);

					// If everything matches we can skip checksumming
					if (Check1 && Check2)
						continue;

					// One of the files failed the check. Re-hash

					if (!Check1)
						State.MainDirCache!.UpdCache(FileLocation);
					if (!Check2)
						State.SyncDirCache!.UpdCache(FileLocation);
				}
				else // Cached objects not found
				{
					byte[] File1Checksum;
					byte[] File2Checksum;
					
					try
					{
						File1Checksum = Cache.Crc64.Compute(File1Path);
						File2Checksum = Cache.Crc64.Compute(File2Path);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to compute checksum: {0}", ex);
						continue;
					}

					State.MainDirCache!.AddCache(FileLocation, new NodeData
					{
						Hash = Cache.Crc64.Compute(File1Path)
					});
					State.SyncDirCache!.AddCache(FileLocation, new NodeData
					{
						Hash = Cache.Crc64.Compute(File2Path)
					});
				}
			}

			// One of the caches changed, check file hashes
			try
			{
				byte[] Hash1 = Cache.Crc64.Compute(File1Path);
				byte[] Hash2 = Cache.Crc64.Compute(File2Path);

				if (!HashesEqual(Hash1, Hash2))
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("* {0}", FileLocation);
					Console.ForegroundColor = ConsoleColor.White;
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