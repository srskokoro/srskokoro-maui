namespace Kokoro;
using System.Runtime.InteropServices;

partial class Class {

	private Enums? _Enums;

	private sealed class Enums : Dictionary<StringKey, List<EnumElem>?> {
		public EnumChanges? Changes;
	}

	private sealed class EnumChanges : Dictionary<StringKey, List<EnumElem>?> { }


	public readonly struct EnumElem {
		private readonly FieldVal _Value;
		private readonly int _Ordinal;

		public readonly FieldVal Value => _Value;
		public readonly int Ordinal => _Ordinal;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public EnumElem(FieldVal value, int ordinal) {
			_Value = value;
			_Ordinal = ordinal;
		}
	}


	public ICollection<StringKey> EnumNames {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Enums?.Keys ?? EmptyEnumNames.Instance;
	}

	private static class EmptyEnumNames {
		internal static readonly Dictionary<StringKey, List<EnumElem>?>.KeyCollection Instance = new(new());
	}

	public void EnsureCachedEnumName(StringKey name) {
		var enums = _Enums;
		if (enums == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enums.TryAdd(name, null);
		return;

	Init:
		_Enums = enums = new();
		goto Set;
	}


	public bool TryGetEnum(StringKey name, [MaybeNullWhen(false)] out List<EnumElem> elems) {
		var enums = _Enums;
		if (enums != null) {
			enums.TryGetValue(name, out elems);
			return elems != null;
		}
		elems = null;
		return false;
	}

	public void SetEnum(StringKey name, List<EnumElem>? elems) {
		var enums = _Enums;
		if (enums == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = enums.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		enums[name] = elems;
		changes[name] = elems;
		return;

	Init:
		_Enums = enums = new();
	InitChanges:
		enums.Changes = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteEnum(StringKey name) => SetEnum(name, null);

	/// <seealso cref="SetEnumAsLoaded(StringKey, List{EnumElem}?)"/>
	[SkipLocalsInit]
	public void SetCachedEnum(StringKey name, List<EnumElem>? elems) {
		var enums = _Enums;
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
		_Enums = enums = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkEnumAsChanged(StringKey)"/> followed by
	/// <see cref="SetCachedEnum(StringKey, List{EnumElem}?)"/>.
	/// </summary>
	public void SetEnumAsLoaded(StringKey name, List<EnumElem>? elems) {
		var enums = _Enums;
		if (enums == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		enums.Changes?.Remove(name);

	Set:
		enums[name] = elems;
		return;

	Init:
		_Enums = enums = new();
		goto Set;
	}


	public void UnmarkEnumAsChanged(StringKey name)
		=> _Enums?.Changes?.Remove(name);

	public void UnmarkEnumsAsChanged()
		=> _Enums?.Changes?.Clear();


	public void UnloadEnum(StringKey name) {
		var enums = _Enums;
		if (enums != null) {
			enums.Changes?.Remove(name);
			enums.Remove(name);
		}
	}

	public void UnloadEnums() {
		var enums = _Enums;
		if (enums != null) {
			enums.Changes = null;
			enums.Clear();
		}
	}
}
