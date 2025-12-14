using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Cache;

internal static class Deserializer
{
	private static long Int64FromByte(byte[] Long)
	{
		return BitConverter.ToInt64(Long);
	}
	private static int Int32FromByte(byte[] Integer)
	{
		return BitConverter.ToInt32(Integer);
	}
	private static short Int16FromByte(byte[] Short)
	{
		return BitConverter.ToInt16(Short);
	}
	private static ushort UInt16FromByte(byte[] Short)
	{
		return BitConverter.ToUInt16(Short);
	}


	// Returns false if Buffer is not filled
	private static bool StreamSafeRead(MemoryStream Stream, byte[] Buffer)
	{
		int Read = Stream.Read(Buffer, 0, Buffer.Length);
		if (Read != Buffer.Length)
			return false;
		return true;
	}

	// Ensures ParentNode has a child list, and then adds the child
	private static void SafeAddChild(Node ParentNode, Node ChildNode)
	{
		if (ParentNode.Children == null)
			ParentNode.Children = new List<Node>();

		ParentNode.Children.Add(ChildNode);
	}

	private static Node? DeserializeFileNode(MemoryStream Stream, Node Parent, int ChunkSize)
	{
		/* At this point, the size and type bytes have been parsed
		*	We just need to focus on everything after that
		*	This is the correct order:
		*		File Size (long, 8-bytes)
		*		Create Date (long, 8-bytes)
		*		Modify Date (long, 8-bytes)
		*		Hash (byte[], 8 bytes)
		*		Name (string)
		*/

		byte[] FileSize = new byte[8];
		byte[] CreateDate = new byte[8];
		byte[] ModifyDate = new byte[8];
		byte[] Hash = new byte[8];

		// The rest of the chunk is for the name
		byte[] Name = new byte[ChunkSize - 33];

		if (!StreamSafeRead(Stream, FileSize) ||
			!StreamSafeRead(Stream, CreateDate) ||
			!StreamSafeRead(Stream, ModifyDate) ||
			!StreamSafeRead(Stream, Hash) ||
			!StreamSafeRead(Stream, Name))
		{
			Console.WriteLine("Failed to serialize vital chunk data");
			return null;
		}

		// All the byte arrays were loaded successfully, deserialize the data contained and return it in a node

		return new Node
		{
			IsDirectory = false,

			Name = Encoding.UTF8.GetString(Name),
			Parent = Parent,

//			CreateDate = new DateTime(Int64FromByte(CreateDate), DateTimeKind.Utc),
//			ModifiedDate = new DateTime(Int64FromByte(ModifyDate), DateTimeKind.Utc),

			CreateDate = new DateTime(Int64FromByte(CreateDate)),
			ModifiedDate = new DateTime(Int64FromByte(ModifyDate)),
			
			Size = Int64FromByte(FileSize),
			Hash = Hash
		};
	}

	// True = success, False = error
	public static bool BytesToCache(MemoryStream Stream, Node CurrentNode, bool SecondRun = false, int Count = int.MaxValue)
	{
		if (!SecondRun)
		{
			if (Stream.ReadByte() != 0x5A)
			{
				Console.WriteLine("Input data is not cache data");
				return false;
			}
			if (Stream.ReadByte() == 0x01)
			{
				throw new NotImplementedException("Compression not implemented");
				// TODO: Compression
			}

			// Read cache statistics

			int DirCount = -1;
			int FileCount = -1;

			{
				byte[] DirBytes = new byte[4];
				if (!StreamSafeRead(Stream, DirBytes))
				{
					Console.WriteLine("Unexpected end of stream whilst reading");
					return false;
				}
				DirCount = Deserializer.Int32FromByte(DirBytes);
			}
			{
				byte[] FileBytes = new byte[4];
				if (!StreamSafeRead(Stream, FileBytes))
				{
					Console.WriteLine("Unexpected end of stream whilst reading");
					return false;
				}
				FileCount = Deserializer.Int32FromByte(FileBytes);
			}

			Console.WriteLine("Cache has {0} directories and {1} files", DirCount, FileCount);

			// At this point, the MemoryStream is in the first byte of the first chunk
			// Hand over to the recursive functionality
			if (!BytesToCache(Stream, CurrentNode, true))
			{
				// Deserialization failed
				// We may have written data to the root node, clear it
				if (CurrentNode.Children != null)
					CurrentNode.Children.Clear();
				return false;
			}
			return true;
		}

		// Begin recursive code
		// Parse chunks
		for (int i = 0; i < Count; i++)
		{
			// This read has to also check for the proper end of the cache file
			byte[] ChunkSize = new byte[2];
			if (!StreamSafeRead(Stream, ChunkSize)) // This is to be expected if we're done with the file
			{
				// We've reached the end of the cache file
				if (ChunkSize[0] == 0xFF)
					return true;

				Console.WriteLine("Unexpected EOF whilst reading size");
				return false;
			}

			int RealSize = Int16FromByte(ChunkSize);
			if (RealSize < 0 || RealSize > 300)
			{
				Console.WriteLine("Invalid chunk size of {0}", RealSize);
				return false;
			}

			byte[] Chunk = new byte[RealSize];
			if (!StreamSafeRead(Stream, Chunk))
			{
				Console.WriteLine("Unexpected EOF whilst reading chunk");
				return false;
			}

			using MemoryStream ChunkStream = new MemoryStream(Chunk);

			// Parse the chunk's header
			switch(ChunkStream.ReadByte())
			{
				case 0x01: // File
					Node? FileNode = DeserializeFileNode(ChunkStream, CurrentNode, RealSize);
					if (FileNode == null)
					{
						Console.WriteLine("Failed to serialize FileNode");
						return false;
					}
					SafeAddChild(CurrentNode, FileNode);
					break;
				case 0x02: // Directory
					byte[] ChildBytes = new byte[2];
					if (!StreamSafeRead(ChunkStream, ChildBytes))
					{
						Console.WriteLine("Failed to parse directory count");
						return false;
					}
					ushort ChildCount = UInt16FromByte(ChildBytes);

					byte[] DirName = new byte[Chunk.Length - 3]; // -3 -> -1 for flag, -2 for child count
					if (!StreamSafeRead(ChunkStream, DirName))
					{
						Console.WriteLine("Failed to parse directory name");
						return false;
					}

					Node DirectoryNode = new Node
					{
						IsDirectory = true,

						Name = Encoding.UTF8.GetString(DirName),
						Parent = CurrentNode
					};

					SafeAddChild(CurrentNode, DirectoryNode);
					if (!BytesToCache(Stream, DirectoryNode, true, ChildCount))
					{
						Console.WriteLine("Recursive call returned false");
						return false;
					}
					break;
				case -1:
					Console.WriteLine("Unexpected EOF whilst reading chunk flag");
					return false;
				default:
					Console.WriteLine("Unknown chunk magic");
					return false;
			}
		}
		return true;
	}
}