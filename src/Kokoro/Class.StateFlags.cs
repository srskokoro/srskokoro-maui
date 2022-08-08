namespace Kokoro;
using CommunityToolkit.HighPerformance.Helpers;

using StateFlagsInt = System.Int32;
using StateFlagsSInt = System.Int32;
using StateFlagsUInt = System.UInt32;

partial class Class {

	private StateFlags _State;

	private const StateFlagsInt StateFlags_1 = 1; // Type must be the same as the enum's underlying type
	private const int StateFlags_NotExists_Shift = sizeof(StateFlags)*8 - 1; // Sets sign bit when used as shift

	[Flags]
	private enum StateFlags : StateFlagsInt {
		NoChanges = 0,

		Change_Uid      = StateFlags_1 << 0,
		Change_ModStamp = StateFlags_1 << 1,
		Change_Ordinal  = StateFlags_1 << 2,
		Change_GroupId  = StateFlags_1 << 3,
		Change_Name     = StateFlags_1 << 4,

		NotExists       = StateFlags_1 << StateFlags_NotExists_Shift,
	}


	public bool Exists {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => (StateFlagsSInt)_State < 0 ? false : true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetCachedExists(bool exists = true) {
		_State = (StateFlags)BitHelper.SetFlag(
			(StateFlagsUInt)_State, StateFlags_NotExists_Shift, !exists);
	}
}
