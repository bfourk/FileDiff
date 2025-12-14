//TODO:
// * Ensure that different backlashes are handled properly
// * Compression

using System;
using System.IO;
using System.Collections.Generic;

namespace Cache;

// Main class for the node object
public class Node
{
	public required bool IsDirectory {get; set;}

	// Common properties

	public required string Name {get; set;}
	public required Node Parent {get; set;}

	// Directory properties

	public List<Node>? Children {get; set;}

	// File properties

	public DateTime? CreateDate {get; set;}
	public DateTime? ModifiedDate {get; set;}
	public long? Size {get; set;}
	public byte[]? Hash {get; set;}
}

// Holds parameters that a program can set
public class NodeData
{
	public DateTime? CreateDate {get; set;}
	public DateTime? ModifiedDate {get; set;}
	public long? Size {get; set;}
	public byte[]? Hash {get; set;}
}

public class DirCache
{
	public Node RootNode;
	public string RootPath;

	public DirCache(string RootPath, string? LoadPath = null)
	{
		this.RootPath = RootPath;

		RootNode = new Node
		{
			Name = "root",
			IsDirectory = true,
			Parent = null!,
			Children = new List<Node>(),
		};

		if (LoadPath == null)
			return;

		// Load cache from file

		byte[]? CacheData = null;
		try
		{
			CacheData = File.ReadAllBytes(LoadPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Failed to load cache data: {0}", ex);
			return;
		}

		MemoryStream Stream = new MemoryStream(CacheData);
		Stream.Position = 0; // I'm not sure if this is necessary

		if (!Deserializer.BytesToCache(Stream, RootNode))
			Console.WriteLine("Failed to deserialize cache: returned null. Skipping");
	}

	private Node CreateDirNode(string Name, Node Parent, List<Node>? Children = null)
	{
		return new Node
		{
			IsDirectory = true,

			Name = Name,
			Parent = Parent,
			Children = Children
		};
	}

	private Node CreateFileNode(string Name, Node Parent, DateTime cDate, DateTime mDate, long Size, byte[] Hash)
	{
		return new Node
		{
			IsDirectory = false,

			Name = Name,
			Parent = Parent,
			CreateDate = cDate,
			ModifiedDate = mDate,
			Size = Size,
			Hash = Hash
		};
	}

	// Get a path in the cache tree
	private Node? GetPath(string RelPath)
	{
		if (RelPath == ".")
			return RootNode;
		if (RootNode.Children == null || RootNode.Children.Count == 0)
			return null;

		string[] spl = RelPath.Split('/');
		int index = 0;

		Node? CurrentNode = RootNode;

		while (true)
		{
			if (CurrentNode.Children == null || CurrentNode.Children.Count == 0)
				return null;

			bool found = false;
			for (int i = 0; i < CurrentNode.Children.Count; i++)
			{
				if (CurrentNode.Children[i].Name == spl[index])
				{
					found = true;
					CurrentNode = CurrentNode.Children[i];
					break;
				}
			}
			if (!found)
				return null;

			index++;
			if (spl.Length == index)
				return CurrentNode;
		}
	}

	// Recursively create a path in cache
	// Returns the final node it creates
	private Node CreatePath(string RelPath)
	{
		// The start of this is pretty much the same to PathExists

		string[] spl = RelPath.Split('/');

		Node CurrentNode = RootNode;
		int index = 0;

		while (true)
		{
			if (index == spl.Length)
				return CurrentNode;

			string CurrentPath = spl[index];
			
			// No children, no need to search we can just add it
			if (CurrentNode.Children == null || CurrentNode.Children.Count == 0)
			{
				Node NewNode = CreateDirNode(CurrentPath, CurrentNode);
				CurrentNode.Children = new List<Node>();
				CurrentNode.Children.Add(NewNode);
				CurrentNode = NewNode;
				index++;
				continue;
			}

			// Check if it already exists
			bool exists = false;
			for (int i = 0; i < CurrentNode.Children.Count; i++)
				if (CurrentNode.Children[i].Name == CurrentPath)
				{
					CurrentNode = CurrentNode.Children[i];
					index++;
					exists = true;
					break;
				}
			if (exists)
				continue;

			// This shouldn't be possible as we already checked above, but compiler wants this
			if (CurrentNode.Children == null)
			{
				Console.WriteLine("This condition shouldn't be possible. Bailing out");
				Environment.Exit(1);
			}

			// It doesn't exist, add it to existing children
			{
				Node NewNode = CreateDirNode(CurrentPath, CurrentNode);
				CurrentNode.Children.Add(NewNode);
				CurrentNode = NewNode;
			}
			index++;
		}
	}

/*

	// Gets and returns file information from FS
	// 		CreateDate,	ModDate,	FileHash
	private (DateTime?, DateTime?, long?, byte[]?) GetFileInformation(string RelPath)
	{
		string RealPath = Path.Join(RootPath, RelPath);
		if (!File.Exists(RealPath))
		{
			Console.WriteLine("Refusing to work with path {0} to cache as file does not exist", RelPath);
			return (null, null, null, null);
		}

		string? DirPath = Path.GetDirectoryName(RelPath);
		string FileName = Path.GetFileName(RelPath);

		if (DirPath == null)
			return (null, null, null, null);

		DateTime? FileCreateDate = null;
		DateTime? FileModDate = null;
		long? FileSize = null;
		byte[]? FileHash = null;

		try
		{
			FileInfo Info = new FileInfo(RealPath);

			FileCreateDate = Info.CreationTime;
			FileModDate = Info.LastWriteTime;
			FileSize = Info.Length;
			FileHash = Crc64.Compute(RealPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Failed to read file information: {0}", ex.ToString());
		}

		if (FileCreateDate == null || FileModDate == null || FileSize == null || FileHash == null)
			return (null, null, null, null);

		return (FileCreateDate, FileModDate, FileSize, FileHash);
	}

*/

	// Public functions

	public void AddCache(string RelPath, NodeData? OverrideData = null)
	{
		if (RelPath.StartsWith('/'))
		{
			Console.WriteLine("AddCache uses relative paths");
			return;
		}
		
		string? DirPath = Path.GetDirectoryName(RelPath);
		string FileName = Path.GetFileName(RelPath);

		DateTime? CreateDate = null;
		DateTime? ModDate = null;
		long? Size = null;
		byte[]? Hash = null;

		string RealPath = Path.Join(RootPath, RelPath);

		try
		{
			FileInfo Info = new FileInfo(RealPath);

/*
			CreateDate = OverrideData == null ? Info.CreationTime : OverrideData.CreateDate == null ? Info.CreationTime : OverrideData.CreateDate;
			ModDate = OverrideData == null ? Info.LastWriteTime : OverrideData.ModifiedDate == null ? Info.LastWriteTime : OverrideData.ModifiedDate;
			Size = OverrideData == null ? Info.Length : OverrideData.Size == null ? Info.Length : OverrideData.Size;
			Hash = OverrideData == null ? Crc64.Compute(RealPath) : OverrideData.Hash == null ? Crc64.Compute(RealPath) : OverrideData.Hash;
*/

			CreateDate = OverrideData?.CreateDate ?? Info.CreationTime;
			ModDate = OverrideData?.ModifiedDate ?? Info.LastWriteTime;
			Size = OverrideData?.Size ?? Info.Length;
			Hash = OverrideData?.Hash ?? Crc64.Compute(RealPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine("AddCache: Failed to read file information from disk for {0}: {1}", RelPath, ex);
			return;
		}

		if (CreateDate == null || ModDate == null || Size == null || Hash == null || DirPath == null)
			return;

		Node Folder = DirPath == "" ? RootNode : CreatePath(DirPath);

		if (Folder.Children == null)
			Folder.Children = new List<Node>();

		Folder.Children.Add(CreateFileNode(FileName, Folder, CreateDate.Value, ModDate.Value, Size.Value, Hash));
	}

	public Node? ReadCache(string RelPath)
	{
		return GetPath(RelPath);
	}

	// Returns true when update is successful, false if not
	public bool UpdCache(string RelPath, NodeData? OverrideData = null)
	{
		Node? Child = GetPath(RelPath);
		if (Child == null)
		{
			Console.WriteLine("UpdCache: Could not find child node");
			return false;
		}

		DateTime? CreateDate = null;
		DateTime? ModDate = null;
		long? Size = null;
		byte[]? Hash = null;

		string RealPath = Path.Join(RootPath, RelPath);

		try
		{
			FileInfo Info = new FileInfo(RealPath);

			CreateDate = OverrideData?.CreateDate ?? Info.CreationTime;
			ModDate = OverrideData?.ModifiedDate ?? Info.LastWriteTime;
			Size = OverrideData?.Size ?? Info.Length;
			Hash = OverrideData?.Hash ?? Crc64.Compute(RealPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine("UpdCache: Failed to read file information from disk for {0}: {1}", RelPath, ex);
			return false;
		}

		if (CreateDate == null || ModDate == null || Size == null || Hash == null)
			return false;

		Child.CreateDate = CreateDate;
		Child.ModifiedDate = ModDate;
		Child.Size = Size;
		Child.Hash = Hash;
		return true;
	}

	// Delete a node from cache
	// Will also recursively delete empty folders in the node's path
	public void DelCache(string RelPath)
	{
		Node? Child = GetPath(RelPath);
		if (Child == null)
		{
			Console.WriteLine("DelCache: Could not find child node");
			return;
		}

		if (Child.Parent.Children == null)
		{
			Console.WriteLine("DelCache WARN: Child's parent has null children. Expect problems");
			return;
		}
		Child.Parent.Children.Remove(Child);

		Node CurrentNode = Child.Parent;

		// Recursively delete empty folders
		while (true)
		{
			if (CurrentNode == RootNode)
				break;

			// Technically null doesn't make sense in this context, but check anyways
			if (CurrentNode.Children != null && CurrentNode.Children.Count >= 1)
				break;

			if (CurrentNode.Parent.Children == null)
			{
				Console.WriteLine("DelCache WARN: CurrentNode's parent has null children. Expect problems");
				return;
			}
			CurrentNode.Parent.Children.Remove(CurrentNode);
			CurrentNode = CurrentNode.Parent;
		}
	}

	// Clears the cache completely
	public void ClearCache()
	{
		if (RootNode.Children == null)
			return;
		RootNode.Children.Clear();
	}

	// Prints the cache tree, along with the create date and hash of cache nodes
	// This should be called by the user with no arguments
	public void DbgPrint(bool SecondRun = false, Node? RunNode = null, int Depth = 0)
	{
		if (!SecondRun)
		{
			if (RootNode.Children == null || RootNode.Children.Count == 0)
			{
				Console.WriteLine("Dbg: Empty cache");
				return;
			}
			for (int i = 0; i < RootNode.Children.Count; i++)
				DbgPrint(true, RootNode.Children[i], 0);

			return;
		}
		if (RunNode == null)
			return;

		Console.Write("DbgPrint: ");

		for (int i = 0; i < Depth; i++)
			Console.Write("-");

		if (!RunNode.IsDirectory)
		{
			// This ignores a compiler null warning
			Console.WriteLine("F {0} {1} {2}", RunNode.Name, RunNode.CreateDate, BitConverter.ToUInt64(RunNode.Hash!, 0).ToString("X16"));
			return;
		}

		Console.WriteLine("D {0}", RunNode.Name);

		if (RunNode.Children == null || RunNode.Children.Count == 0)
			return;

		for (int i = 0; i < RunNode.Children.Count; i++)
			DbgPrint(true, RunNode.Children[i], Depth + 1);
	}

	public byte[]? Serialize(bool Compression = false)
	{
		if (RootNode == null)
		{
			Console.WriteLine("Cannot serialize, RootNode is NULL. Something has gone terribly wrong");
			return null;
		}
		return Serializer.CacheToBytes(RootNode, Compression);
	}
}