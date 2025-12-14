using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Cache;

// This class holds temporary information during a serialization process
internal class SerializeState
{
	public required Node RootNode {get; set;}

//	public int CurrentFolderID {get; set;} = 0;
	public int DirectoryCount {get; set;} = 0;
	public int FileCount {get; set;} = 0;

	public MemoryStream Stream {get; set;} = new MemoryStream();
}

internal static class Serializer
{
	private static byte[] Int64ToByte(long Long)
	{
		return BitConverter.GetBytes(Long);
	}
	private static byte[] Int32ToByte(int Integer)
	{
		return BitConverter.GetBytes(Integer);
	}
	private static byte[] Int16ToByte(short Short)
	{
		return BitConverter.GetBytes(Short);
	}
	private static byte[] UInt16ToByte(ushort Short)
	{
		return BitConverter.GetBytes(Short);
	}

	private static void AppendToStream(MemoryStream Stream, byte[] Data)
	{
		Stream.Position = Stream.Length;
		Stream.Write(Data, 0, Data.Length);
	}

	public static byte[]? CacheToBytes(Node CurrentNode, bool GZip = false, bool SecondRun = false, SerializeState? State = null)
	{
		if (!SecondRun)
		{ // CurrentNode is RootNode on the first run
			Console.WriteLine("Begin Serialization");
			SerializeState NewState = new SerializeState
			{
				RootNode = CurrentNode,
			};

			// Begin constructing the first header of the file
			// TODO: Compression
			AppendToStream(NewState.Stream, new byte[] { 0x5A, 0x00} );

			// We don't know the values in the header right now, so set them to nothing and update later
			//	File Count Int32, Directory Count Int32
			AppendToStream(NewState.Stream, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00} );

			CacheToBytes(CurrentNode, GZip, true, NewState); // Start recursion loop
			AppendToStream(NewState.Stream, new byte[] {0xFF}); // EOF indicator

			// Now everything is parsed. Make it into a contiguous structure and return it

			byte[] ret = NewState.Stream.ToArray();
			NewState.Stream.Dispose();

//			Console.WriteLine("Dir Count: {0}\nFile Count: {1}\nMax Dir ID: {2}", NewState.DirectoryCount, NewState.FileCount, /*NewState.CurrentFolderID*/0);

			// Now that we know the header values, update them

			{
				byte[] DirCnt = Int32ToByte(NewState.DirectoryCount);
				ret[2] = DirCnt[0];
				ret[3] = DirCnt[1];
				ret[4] = DirCnt[2];
				ret[5] = DirCnt[3];
			}
			{
				byte[] FileCnt = Int32ToByte(NewState.FileCount);
				ret[6] = FileCnt[0];
				ret[7] = FileCnt[1];
				ret[8] = FileCnt[2];
				ret[9] = FileCnt[3];
			}

			Console.WriteLine("Serialization OK");
			return ret;
		}

		//
		// Recursion Run
		//

		if (State == null)
			return null;

		if (CurrentNode.Children == null || CurrentNode.Children.Count == 0)
		{
			Console.WriteLine("Node has no children, skipping");
			return null;
		}

		// Parse files first
		for (int i = 0; i < CurrentNode.Children.Count; i++)
		{
			Node Child = CurrentNode.Children[i];
			if (Child.IsDirectory) // Skip directories
				continue;

			if (Child.Name == "")
			{
				Console.WriteLine("Skipping file with no name");
				continue;
			}
			if (Child.CreateDate == null || Child.ModifiedDate == null ||
				Child.Size == null || Child.Hash == null)
			{
				Console.WriteLine("Skipping child with missing information");
				continue;
			}

			byte[] ChildNameBytes = Encoding.UTF8.GetBytes(Child.Name);

			// The string length can actually be different than the byte length due to UTF8 funness
			// So we have to check the BYTE length
			AppendToStream(State.Stream, Int16ToByte( (short)(33 + ChildNameBytes.Length) ));
			AppendToStream(State.Stream, new byte[] { 0x01 }); // File flag
			AppendToStream(State.Stream, Int64ToByte(Child.Size.Value));
			AppendToStream(State.Stream, Int64ToByte(Child.CreateDate.Value.Ticks));
			AppendToStream(State.Stream, Int64ToByte(Child.ModifiedDate.Value.Ticks));
			AppendToStream(State.Stream, Child.Hash);
			AppendToStream(State.Stream, ChildNameBytes);

			if (i == CurrentNode.Children.Count) // This is the last child
				AppendToStream(State.Stream, new byte[] { 0xFF }); // EOF for the chunk

			State.FileCount++;
		}

		// Now parse directories
		for (int i = 0; i < CurrentNode.Children.Count; i++)
		{
			Node Child = CurrentNode.Children[i];
			if (!Child.IsDirectory) // Skip files
				continue;

			if (Child.Name == "")
			{
				Console.WriteLine("Skipping directory with no name");
				continue;
			}

			if (Child.Children == null || Child.Children.Count == 0) // Empty directories shouldn't be possible, but skip em if they exist
			{
				Console.WriteLine("Skipping empty directory {0}", Child.Name);
				continue;
			}

			// Prepare MemoryStream for the next recursion

			AppendToStream(State.Stream, Int16ToByte( (short)(3 + Child.Name.Length) ));
			AppendToStream(State.Stream, new byte[] { 0x02 }); // Directory flag
			AppendToStream(State.Stream, UInt16ToByte((ushort)(Child.Children.Count)));
			AppendToStream(State.Stream, Encoding.UTF8.GetBytes(Child.Name));

			// Start next recursions
			CacheToBytes(Child, GZip, true, State); // Start recursion loop
			State.DirectoryCount++;
		}

		return null; // Recursion steps don't return anything
	}
}