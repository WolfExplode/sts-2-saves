using System;
using System.Linq;
using System.Text;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using NyMod.Saves.Features.SaveArchive.Models;
using NyMod.Saves.Infrastructure.Diagnostics;

namespace NyMod.Saves.Features.SaveArchive.Logic;

internal sealed class SaveArchiveSummaryFactory
{
	public SaveArchiveSummary Create(byte[] payloadBytes)
	{
		ModDiagnostics.Trace("Reading existing save summary");
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string @string = Encoding.UTF8.GetString(payloadBytes);
			ReadSaveResult<SerializableRun> val = JsonSerializationUtility.FromJson<SerializableRun>(@string);
			if (!val.Success || val.SaveData == null)
			{
				SaveArchiveSummary obj = new SaveArchiveSummary
				{
					SummaryAvailable = false
				};
				string? text = val.ErrorMessage;
				if (text == null)
				{
					ReadSaveStatus status = val.Status;
					text = ((object)status).ToString();
				}
				obj.ErrorMessage = text;
				return obj;
			}
			SerializableRun saveData = val.SaveData;
			SerializablePlayer val2 = saveData.Players.FirstOrDefault();
			SaveArchiveSummary obj2 = new SaveArchiveSummary
			{
				SummaryAvailable = true,
				CharacterIds = saveData.Players.Select((SerializablePlayer player) => ((object)player.CharacterId)?.ToString() ?? "unknown").ToList(),
				PlayerCount = saveData.Players.Count,
				SaveTime = saveData.SaveTime,
				StartTime = saveData.StartTime,
				RunTime = saveData.RunTime,
				Ascension = saveData.Ascension,
				CurrentActIndex = saveData.CurrentActIndex,
				EstimatedFloor = saveData.VisitedMapCoords.Count,
				CurrentHp = ((val2 != null) ? new int?(val2.CurrentHp) : null),
				MaxHp = ((val2 != null) ? new int?(val2.MaxHp) : null),
				Gold = ((val2 != null) ? new int?(val2.Gold) : null)
			};
			PlatformType platformType = saveData.PlatformType;
			obj2.PlatformType = ((object)platformType).ToString();
			obj2.DailyTime = saveData.DailyTime;
			return obj2;
		}
		catch (Exception ex)
		{
			return new SaveArchiveSummary
			{
				SummaryAvailable = false,
				ErrorMessage = ex.Message
			};
		}
	}
}
