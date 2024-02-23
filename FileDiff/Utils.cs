using System.Security.Cryptography;

namespace FileDiff;

internal static class Util
{
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
}