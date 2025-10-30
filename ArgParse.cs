using System;

namespace FileDiff;

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