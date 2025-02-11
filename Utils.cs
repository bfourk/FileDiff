using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace FileDiff;

internal static class Util
{
	public static bool AlwaysYes = false;

	//
	// File related functions
	//

	private static bool CompareHash(byte[] hash1, byte[] hash2)
	{
		// Compares if byte array hash1 and hash2 contain the exact same data
		int CharIndex = 0;
		if (hash1.Length != hash2.Length)
			return false;
		while (true)
		{
			if (CharIndex == hash1.Length)
				return true;
			if (hash1[CharIndex] != hash2[CharIndex])
				return false;
			CharIndex++;
		}
	}
	public static bool CompareFiles(string path1, string path2)
	{
		// Check size first. If size is different, we know the file changed
		FileInfo f1Info = new FileInfo(path1);
		FileInfo f2Info = new FileInfo(path2);

		if (f1Info.Length != f2Info.Length)
			return false;

		// Size is the same, check hashes

		byte[] Hash1;
		byte[] Hash2;

		using (SHA256 sha = SHA256.Create()) // NOTE: could use MD5 for better performance, SHA256 is ~4.5 seconds slower in total
		{
			using (FileStream stream = File.OpenRead(path1))
				Hash1 = sha.ComputeHash(stream);
			using (FileStream stream = File.OpenRead(path2))
				Hash2 = sha.ComputeHash(stream);
		}
		return CompareHash(Hash1, Hash2);
	}
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

	public static void RecursiveRemove(List<string> List1, List<string> List2)
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
}

internal class ArgParse
{
	private string[]? args;
	public ArgParse(string[] args)
	{
		this.args = args;
	}

	// Look through the args array, and if the argument is found, return the value
	public string? GetArg(string arg)
	{
		// TODO:
		// Implement support for spaces in arguments i.e --server=test 1 2 3 or --server test 1 2 3
		if (args == null)
		{
			Console.WriteLine("Error: args not set");
			return null;
		}
		for (int i = 0; i < args.Length; i++)
		{
			string a = args[i];
			if (a.StartsWith(arg) ||
				a.StartsWith(string.Format("--{0}", arg)) ||
				a.StartsWith(string.Format("-{0}",arg)))
			{
				if (i + 1 == args.Length)
					return a;
				if (args[i + 1].Substring(0,1) == "-")
				{
					if (a.Contains("="))
						return a.Split("=")[1];
					return a;
				}
				return args[i + 1];
			}
		}
		return null;
	}

	public string? GetArg(params string[] arg)
	{
		for (int i = 0; i < arg.Length; i++)
		{
			string? ret = GetArg(arg[i]);
			if (ret != null)
				return ret;
		}
		return null;
	}
}