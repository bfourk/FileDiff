using System;
using System.IO;
using System.IO.Compression;

namespace Cache;

internal static class GZip
{
	public static byte[] Compress(byte[] input)
	{
		using MemoryStream ret = new MemoryStream();
		using GZipStream gz = new GZipStream(ret, CompressionMode.Compress);

		gz.Write(input, 0, input.Length);
		gz.Flush();

		return ret.ToArray();
	}

	public static byte[] Decompress(byte[] input)
	{
		using MemoryStream src = new MemoryStream(input);
		using GZipStream stream = new GZipStream(src, CompressionMode.Decompress);
		using MemoryStream ret = new MemoryStream();

		stream.CopyTo(ret);

		return ret.ToArray();
	}
}