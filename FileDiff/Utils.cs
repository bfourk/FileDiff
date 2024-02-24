using System.Security.Cryptography;

namespace FileDiff;

internal static class Util
{

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

	//
	// Misc. Functions
	//

	public static bool RequestYN(string arg)
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