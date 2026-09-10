using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;

namespace NyMod.Saves.Features.SaveArchive.Integration;

internal sealed class PauseMenuHookState
{
	public required NPauseMenuButton SaveButton { get; init; }

	public required NPauseMenuButton LoadButton { get; init; }
}
