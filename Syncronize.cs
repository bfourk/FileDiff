using System;
using System.IO;

namespace FileDiff;

internal static class Synchronizer
{
	private static bool DoCache => FDiff.DoCache;

	public static void Sync(SyncState State, bool DoGarbage)
	{
		if ((State.FileAdditions.Count + State.DirAdditions.Count) == 0)
			Console.WriteLine("No Additions, Skipping");
		else
			if (Util.RequestYN("Synchronize Additions?"))
			{
				foreach (string add in State.DirAdditions)
				{
					string DirPath = Path.Join(State.SyncDirectory, add);
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
				foreach (string add in State.FileAdditions)
				{
					string Path1 = Path.Join(State.MainDirectory, add);
					string Path2 = Path.Join(State.SyncDirectory, add);
					Console.WriteLine("+ {0}", add);
					try
					{
						File.Copy(Path1, Path2);
						if (DoCache)
							State.SyncDirCache!.AddCache(add);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to copy file {0}\nReason: {1}", add, ex.ToString());
						continue;
					}
				}
			}

		if (State.FileChanges.Count == 0)
			Console.WriteLine("No Changes, Skipping");
		else
			if (Util.RequestYN("Synchronize Changes?"))
				foreach (string ch in State.FileChanges)
				{
					string Path1 = Path.Join(State.MainDirectory, ch);
					string Path2 = Path.Join(State.SyncDirectory, ch);
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
					if (DoCache)
						State.SyncDirCache!.UpdCache(ch);
				}

		int DuplicateInc = 0;
		if ((State.DirDeletions.Count + State.FileDeletions.Count) == 0)
			Console.WriteLine("No Deletions, Skipping");
		else
			if (Util.RequestYN("Synchronize Deletions?"))
			{
				string GarbagePath = Path.Join(State.SyncDirectory,".DiffTrash");
				if (!Directory.Exists(GarbagePath) && DoGarbage)
					Directory.CreateDirectory(GarbagePath);

				// Files
				foreach (string del in State.FileDeletions)
				{
					if (DoGarbage)
					{
						DuplicateInc++;
						string Path1 = Path.Join(State.SyncDirectory, del);
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
								File.Move(Path1, Path.Join(GarbagePath, string.Format("{0}-{1}", DuplicateInc.ToString(), del)));
							}
							else
								File.Move(Path1, NewPath);
						}
						catch (Exception ex)
						{
							Console.WriteLine("Failed to trash file {0}\nReason: {1}", del, ex.ToString());
							continue;
						}
						if (DoCache)
							State.SyncDirCache!.DelCache(del);
						continue;
					}
					// Garbage is disabled, just delete it
					try
					{
						File.Delete(Path.Join(State.SyncDirectory, del));
						if (DoCache)
							State.SyncDirCache!.DelCache(del);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to delete file {0}\nReason: {1}", del, ex.ToString());
					}
				}
				// Folders
				foreach (string del in State.DirDeletions)
				{
					if (DoGarbage)
					{
						try
						{
							string DirPath = Path.Join(State.SyncDirectory, del);
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
						Directory.Delete(Path.Join(State.SyncDirectory, del), true);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Failed to delete directory {0}\nReason: {1}", del, ex.ToString());
					}
				}
			}
	}
}