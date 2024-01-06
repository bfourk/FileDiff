using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace FileDiff
{
	public class FDiff
	{
		private static bool CompareHash(byte[] hash1, byte[] hash2)
		{
			// Compares if byte array hash1 and hash2 contain the exact same data
			int CharIndex = 0;
			if (hash1.Length != hash2.Length)
				return false;
			while (true)
			{
				if (CharIndex+1 >= hash1.Length)
					return true;
				byte b1 = hash1[CharIndex];
				byte b2 = hash2[CharIndex];
				if (b1 != b2)
					return false;
				CharIndex++;
			}
		}
		private static void Crawl(string Root, string NewDir, HashSet<string> Output)
		{
			string NewPath = Path.Join(Root,NewDir);
			string[] Files = Directory.GetFiles(NewPath);
			string[] Directories = Directory.GetDirectories(NewPath);
			foreach (string File in Files)
				// Add file to the list
				Output.Add(File.Substring(File.IndexOf("/./")+3));
			foreach (string Dir in Directories)
				// Re-crawl subdirectory
				Crawl(NewPath, Dir.Split("/").Last(), Output);
		}
		public static void Main(string[] args)
		{
			Stopwatch sw = new Stopwatch(); // For calculating the total time
			sw.Start();
			Console.Write("Input Directory 1: ");
			string? MainDirectory = Console.ReadLine();
			Console.Write("\nInput Directory 2: ");
			string? SyncDirectory = Console.ReadLine();
			if (MainDirectory == null || SyncDirectory == null)
			{
				Environment.Exit(1);
				return;
			}
			// Confirm with user if both directories are the correct ones
			while (true)
			{
				Console.Write("\n\nDirectory 1: \"{0}\"\nDirectory 2: \"{1}\"\nIs this correct? [Y/n] [ ]\b\b", MainDirectory, SyncDirectory);
				char tChar = Convert.ToChar(Console.ReadKey().Key);
				Console.Write("\n");
				if (Char.ToLower(tChar) == 'n')
				{
					Console.WriteLine("Exiting");
					Environment.Exit(0);
				}
				if (Char.ToLower(tChar) == 'y')
					break;
			}

			// Build two directory lists of MainDirectory and SyncDirectory
			HashSet<string> MainDirectoryList = new HashSet<string>();
			HashSet<string> SyncDirectoryList = new HashSet<string>();

			Console.WriteLine("Building Directory List 1");
			Crawl(MainDirectory,".",MainDirectoryList);
			Console.WriteLine("Building Directory List 2");
			Crawl(SyncDirectory,".",SyncDirectoryList);

			int MainDirectoryCnt = MainDirectoryList.Count; // For the percentage
			Console.WriteLine("Completed:\nMain Directory File Count: {0}\nTarget Directory File Count: {1}\n",MainDirectoryList.Count, SyncDirectoryList.Count);

			int DirectoryIndex = 0;
			int Additions = 0;
			int Deletions = 0;
			int Changes = 0;

			// Search for additions/changes
			foreach(string FileLocation in MainDirectoryList)
			{
				// If the second directory contains file "FileLocation"
				if (!SyncDirectoryList.Contains(FileLocation))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("+ {0}",FileLocation);
						Additions++;
					}
				else
				{
					// Hash the two files, check if different
					byte[] Hash1;
					byte[] Hash2;
					using (SHA256 sha = SHA256.Create()) // NOTE: could use MD5 for better performance, SHA256 is ~4.5 seconds slower in total
					{
						string File1Path = Path.Join(MainDirectory, FileLocation);
						string File2Path = Path.Join(SyncDirectory, FileLocation);
						try
						{
							using (var stream = File.OpenRead(File1Path))
								Hash1 = sha.ComputeHash(stream);
							using (var stream = File.OpenRead(File2Path))
								Hash2 = sha.ComputeHash(stream);
						}
						catch (Exception ex)
						{
							Console.WriteLine("WARN: Failed to read file {0}/{1}\nStacktrace: {2}",File1Path, File2Path, ex.ToString());
							continue;
						}
					}
					if (!CompareHash(Hash1, Hash2))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine("* {0}", FileLocation);
							Changes++;
						}
				}

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
			}

			// Search for deletions
			foreach(string FileLocation in SyncDirectoryList)
				if (!MainDirectoryList.Contains(FileLocation))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("- {0}",FileLocation);
						Deletions++;
					}

			Console.ForegroundColor = ConsoleColor.White;
			int Total = Additions + Deletions + Changes;

			if (Total == 0)
				Console.WriteLine("No changes!");
			else
				Console.WriteLine("{0} Addition(s)\n{1} Deletion(s)\n{2} Modification(s)\n\n\t{3} Total", Additions, Deletions, Changes, Total);
			sw.Stop();
			Console.WriteLine("Finished in {0}",Math.Floor(sw.Elapsed.TotalSeconds * 100) / 100);
		}
	}
}