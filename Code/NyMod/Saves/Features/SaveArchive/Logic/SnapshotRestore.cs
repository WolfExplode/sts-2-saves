using System;
using System.Collections.Generic;
using System.IO;
using NyMod.Saves.Infrastructure.Persistence;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class SnapshotRestore
{
	private readonly string _activePath;
	private readonly byte[] _payload;
	private readonly byte[]? _previousSave;
	private readonly byte[]? _previousBackup;

	public SnapshotRestore(string activePath, byte[] payload)
	{
		_activePath = activePath;
		_payload = payload;
		_previousSave = File.Exists(activePath) ? File.ReadAllBytes(activePath) : null;
		_previousBackup = File.Exists(activePath + ".backup") ? File.ReadAllBytes(activePath + ".backup") : null;
	}

	public void Install()
	{
		AtomicFile.Write(_activePath, _payload);
		AtomicFile.Write(_activePath + ".backup", _payload);
	}

	public void RollBack()
	{
		var failures = new List<Exception>();
		RestoreFile(_activePath, _previousSave, failures);
		RestoreFile(_activePath + ".backup", _previousBackup, failures);
		if (failures.Count > 0)
		{
			throw new AggregateException("Could not recover the previous save files.", failures);
		}
	}

	private static void RestoreFile(string path, byte[]? previous, List<Exception> failures)
	{
		try
		{
			if (previous == null)
			{
				File.Delete(path);
			}
			else
			{
				AtomicFile.Write(path, previous);
			}
		}
		catch (Exception ex)
		{
			failures.Add(ex);
		}
	}
}
