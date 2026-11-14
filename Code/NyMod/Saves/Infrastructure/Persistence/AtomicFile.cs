using System;
using System.IO;

namespace NyMod.Saves.Infrastructure.Persistence;

internal static class AtomicFile
{
	public static void Write(string path, byte[] contents)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				stream.Write(contents);
				stream.Flush(flushToDisk: true);
			}
			File.Move(temporaryPath, path, overwrite: true);
		}
		finally
		{
			File.Delete(temporaryPath);
		}
	}
}
