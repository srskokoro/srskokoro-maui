namespace Kokoro;
using System.Runtime.InteropServices;

partial class Class {

	private EnumGroups? _EnumGroups;

	private sealed class EnumGroups : Dictionary<StringKey, List<EnumInfo>?> {
		public EnumGroupChanges? Changes;
	}

	private sealed class EnumGroupChanges : Dictionary<StringKey, List<EnumInfo>?> { }


	public readonly struct EnumInfo {
		private readonly FieldVal _Value;
		private readonly int _Ordinal;

		public readonly FieldVal Value => _Value;
		public readonly int Ordinal => _Ordinal;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public EnumInfo(FieldVal value, int ordinal) {
			_Value = value;
			_Ordinal = ordinal;
		}
	}


	public ICollection<StringKey> EnumGroupNames {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _EnumGroups?.Keys ?? EmptyEnumGroupNames.Instance;
	}

	private static class EmptyEnumGroupNames {
		internal static readonly Dictionary<StringKey, List<EnumInfo>?>.KeyCollection Instance = new(new());
	}

	public void EnsureCachedEnumGroupName(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enumGroups.TryAdd(name, null);
		return;

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}


	public bool TryGetEnumGroup(StringKey name, [MaybeNullWhen(false)] out List<EnumInfo> elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.TryGetValue(name, out elems);
			return elems != null;
		}
		elems = null;
		return false;
	}

	public List<EnumInfo>? GetEnumGroup(StringKey name) {
		var enums = _EnumGroups;
		if (enums != null) {
			enums.TryGetValue(name, out var elems);
			return elems;
		}
		return null;
	}

	public void SetEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = enumGroups.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		enumGroups[name] = elems;
		changes[name] = elems;
		return;

	Init:
		_EnumGroups = enumGroups = new();
	InitChanges:
		enumGroups.Changes = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteEnumGroup(StringKey name) => SetEnumGroup(name, null);

	/// <seealso cref="SetEnumGroupAsLoaded(StringKey, List{EnumInfo}?)"/>
	[SkipLocalsInit]
	public void SetCachedEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enums = _EnumGroups;
		if (enums == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enums[name] = elems;

		{
			var changes = enums.Changes;
			// Optimized for the common case
			if (changes == null) {
				return;
			} else {
				ref var elemsRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref elemsRef)) {
					elemsRef = elems;
				}
				return;
			}
		}

	Init:
		_EnumGroups = enums = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkEnumGroupAsChanged(StringKey)"/> followed by
	/// <see cref="SetCachedEnumGroup(StringKey, List{EnumInfo}?)"/>.
	/// </summary>
	public void SetEnumGroupAsLoaded(StringKey name, List<EnumInfo>? elems) {
		var enums = _EnumGroups;
		if (enums == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		enums.Changes?.Remove(name);

	Set:
		enums[name] = elems;
		return;

	Init:
		_EnumGroups = enums = new();
		goto Set;
	}


	public void UnmarkEnumGroupAsChanged(StringKey name)
		=> _EnumGroups?.Changes?.Remove(name);

	public void UnmarkEnumGroupsAsChanged()
		=> _EnumGroups?.Changes?.Clear();


	public void UnloadEnumGroup(StringKey name) {
		var enums = _EnumGroups;
		if (enums != null) {
			enums.Changes?.Remove(name);
			enums.Remove(name);
		}
	}

	public void UnloadEnumGroups() {
		var enums = _EnumGroups;
		if (enums != null) {
			enums.Changes = null;
			enums.Clear();
		}
	}
}
