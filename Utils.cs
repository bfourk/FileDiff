using System;
using System.IO;
using System.Collections.Generic;

using Cache;

namespace FileDiff;

internal static class Util
{
	public static bool AlwaysYes = false;

	//
	// File related functions
	//

	// This function recreates a directory tree for trash
	public static void RecreateDirectoryTree(string GarbagePath, string? path)
	{
		string? DirName = Path.GetDirectoryName(path);
		if (DirName == null)
			return;
		string[] WithoutFile = DirName.Split("/");
		string CurrentPath = "";
		foreach (string str in WithoutFile)
		{
			if (str == "")
				continue;
			CurrentPath += string.Format("{0}/",str);
			if (!CurrentPath.Contains(".DiffTrash"))
				continue;
			int index = CurrentPath.IndexOf(".DiffTrash");
			Directory.CreateDirectory(Path.Join(GarbagePath,CurrentPath.Substring(index + 11)));
		}
	}

	//
	// Misc. Functions
	//

	// Asks the console a yes or no question.
	// Returns true for yes, false for no
	public static bool RequestYN(string arg)
	{
		if (AlwaysYes)
			return true;

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

	// Removes all entries from List2 that start with any string in List1
	public static void IterativeRemove(List<string> List1, List<string> List2)
	{
		while (true)
		{
			bool changed = false;
			foreach (string l1Str in List1)
			{
				foreach (string l2Str in List2)
					if (l1Str != l2Str && l2Str.StartsWith(l1Str))
					{
						List2.Remove(l2Str);
						changed = true;
						break;
					}
				if (changed)
					break;
			}
			if (!changed)
				break;
		}
	}

	public static void PruneCache(string RealPath, DirCache Cache)
	{

	}
}