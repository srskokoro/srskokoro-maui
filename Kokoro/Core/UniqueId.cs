using Kokoro.Util;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Kokoro;

public sealed class UniqueId : IComparable, IComparable<UniqueId>, IEquatable<UniqueId> {
	public static int DataSize => Data.Size;

	[StructLayout(LayoutKind.Explicit, Size = Size)]
	private unsafe struct Data {
		public const int Size = 2*sizeof(ulong); // 16

		[FieldOffset(0)] public fixed byte Bytes[Size];

		[FieldOffset(0*sizeof(ulong))] public ulong High;
		[FieldOffset(1*sizeof(ulong))] public ulong Low;

		[FieldOffset(0*sizeof(uint))] public uint U3;
		[FieldOffset(1*sizeof(uint))] public uint U2;
		[FieldOffset(2*sizeof(uint))] public uint U1;
		[FieldOffset(3*sizeof(uint))] public uint U0;

		public Span<byte> Span {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => MemoryMarshal.CreateSpan(ref Bytes[0], Size);
		}
	}

	// Don't mark as `readonly` (or every access would cause a defensive copy)
	private Data _Data;

	public ReadOnlySpan<byte> Span {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _Data.Span;
	}

	public ulong HighBits {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(_Data.High) : _Data.High;
	}

	public ulong LowBits {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(_Data.Low) : _Data.Low;
	}

	public (ulong HighBits, ulong LowBits) BitsPair {
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => (HighBits, LowBits);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create() {
		var uid = new UniqueId();
		RandomNumberGenerator.Fill(uid._Data.Span);
		return uid;
	}

	public static UniqueId Create(ReadOnlySpan<byte> bytes) => new(bytes);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId CreateLenient(ReadOnlySpan<byte> bytes) {
		var uid = new UniqueId();
		var dest = uid._Data.Span;
		if (Data.Size > bytes.Length) {
			dest = dest[(Data.Size - bytes.Length)..];
		} else {
			bytes = bytes[..Data.Size];
		}
		bytes.CopyTo(dest);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create<TState>(TState state, SpanAction<byte, TState> action) {
		var uid = new UniqueId();
		action(uid._Data.Span, state);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(Guid guid) {
		var uid = new UniqueId();
		guid.TryWriteUuidBytes(uid._Data.Span);
		return uid;
	}

	public static UniqueId Create(ulong highBits, ulong lowBits) => new(highBits, lowBits);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(string base58Str) => ParseExact(base58Str);

	public static UniqueId CreateLenient(string base58Str) => Parse(base58Str);


	private static readonly UniqueId _Empty = new();

	public static UniqueId Empty => _Empty;

	public bool IsEmpty => _Data.High == 0 && _Data.Low == 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty([NotNullWhen(false)] UniqueId? uid) => uid is null || uid.IsEmpty;


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId() { }

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(ReadOnlySpan<byte> bytes) {
		if (Data.Size != bytes.Length) {
			throw AOORE_NeedsExactlyDataSize(nameof(bytes));
		}
		bytes.CopyTo(_Data.Span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(ulong highBits, ulong lowBits) {
		if (BitConverter.IsLittleEndian) {
			_Data.High = BinaryPrimitives.ReverseEndianness(highBits);
			_Data.Low = BinaryPrimitives.ReverseEndianness(lowBits);
		} else {
			_Data.High = highBits;
			_Data.Low = lowBits;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(uint u3, uint u2, uint u1, uint u0) {
		if (BitConverter.IsLittleEndian) {
			_Data.U3 = BinaryPrimitives.ReverseEndianness(u3);
			_Data.U2 = BinaryPrimitives.ReverseEndianness(u2);
			_Data.U1 = BinaryPrimitives.ReverseEndianness(u1);
			_Data.U0 = BinaryPrimitives.ReverseEndianness(u0);
		} else {
			_Data.U3 = u3;
			_Data.U2 = u2;
			_Data.U1 = u1;
			_Data.U0 = u0;
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] ToByteArray() => Span.ToArray();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBytes(Span<byte> destination) => Span.TryCopyTo(destination);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void WriteBytes(Span<byte> destination) => Span.CopyTo(destination);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public byte[] WriteBytes(byte[] destination) {
		WriteBytes(destination.AsSpan());
		return destination;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Guid ToGuid() => UuidUtil.GuidFromUuid(Span);

	// --

	public static int Base58Size => _Base58Size;

	// Allocate enough space in big-endian base 58 representation
	// - See, https://github.com/bitcoin/bitcoin/blob/1e7564eca8a688f39c75540877ec3bdfdde766b1/src/base58.cpp#L96
	// - Equiv. to, ceil(N ceil(log(256) / log(58), 0.0001))
	private const int _Base58Size = (Data.Size * 13657 - 1) / 10000 + 1;

	private static ReadOnlySpan<char> Base58EncodingMap => "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
	private const char _Base58Pad = '1'; // 0 in base 58

	// Relies on C# compiler optimization to reference static data
	// See also, https://github.com/dotnet/csharplang/issues/5295
	private static ReadOnlySpan<sbyte> Base58DecodingMap => new sbyte[256] {
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1, 0, 1, 2, 3, 4, 5, 6,  7, 8,-1,-1,-1,-1,-1,-1,
		-1, 9,10,11,12,13,14,15, 16,-1,17,18,19,20,21,-1,
		22,23,24,25,26,27,28,29, 30,31,32,-1,-1,-1,-1,-1,
		-1,33,34,35,36,37,38,39, 40,41,42,43,-1,44,45,46,
		47,48,49,50,51,52,53,54, 55,56,57,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	};

	/// <summary>
	/// Strategy: Treats the underlying data as a 128-bit unsigned integer then converts it to a base 58 integer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private void UnsafeWriteBase58Chars_Inline(Span<char> destination) {
		uint u3, u2, u1, u0;

		if (BitConverter.IsLittleEndian) {
			u3 = BinaryPrimitives.ReverseEndianness(_Data.U3);
			u2 = BinaryPrimitives.ReverseEndianness(_Data.U2);
			u1 = BinaryPrimitives.ReverseEndianness(_Data.U1);
			u0 = BinaryPrimitives.ReverseEndianness(_Data.U0);
		} else {
			u3 = _Data.U3;
			u2 = _Data.U2;
			u1 = _Data.U1;
			u0 = _Data.U0;
		}

		// Get references to avoid unnecessary range checking
		ref char mapRef = ref MemoryMarshal.GetReference(Base58EncodingMap);
		ref char destRef = ref MemoryMarshal.GetReference(destination);

		Debug.Assert(_Base58Size <= destination.Length);
		for (int i = _Base58Size; i-- > 0;) {
			ulong q, v;
			ulong c; // carry

			q = u3 / 58;
			c = u3 - q * 58; // mod 58
			u3 = (uint)q;

			v = u2 | (c << 32);
			q = v / 58;
			c = v - q * 58;
			u2 = (uint)q;

			v = u1 | (c << 32);
			q = v / 58;
			c = v - q * 58;
			u1 = (uint)q;

			v = u0 | (c << 32);
			q = v / 58;
			c = v - q * 58;
			u0 = (uint)q;

			Debug.Assert(i < _Base58Size);
			Debug.Assert(c < 58, $"[{i}] Carry: {c} (0x{c:X})", null);

			Unsafe.Add(ref destRef, i) = Unsafe.Add(ref mapRef, (int)c);
		}

		Debug.Assert(u3 == 0, $"{nameof(u3)}: {u3} (0x{u3:X})", null);
		Debug.Assert(u2 == 0, $"{nameof(u2)}: {u2} (0x{u2:X})", null);
		Debug.Assert(u1 == 0, $"{nameof(u1)}: {u1} (0x{u1:X})", null);
		Debug.Assert(u0 == 0, $"{nameof(u0)}: {u0} (0x{u0:X})", null);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void UnsafeWriteBase58Chars(Span<char> destination)
		=> UnsafeWriteBase58Chars_Inline(destination);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBase58Chars(Span<char> destination) {
		if (_Base58Size > destination.Length) return false;
		UnsafeWriteBase58Chars(destination);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void WriteBase58Chars(Span<char> destination) {
		if (_Base58Size > destination.Length) {
			throw AOORE_NeedsAtLeastBase58Size(nameof(destination));
		}
		UnsafeWriteBase58Chars_Inline(destination);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override string ToString() => string.Create(_Base58Size, this,
			static (destination, @this) => @this.UnsafeWriteBase58Chars(destination));


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static UniqueId? ReadBase58Chars_Inline(ReadOnlySpan<char> source) {
		uint u3 = 0, u2 = 0, u1 = 0, u0 = 0;

		// Get references to avoid unnecessary range checking
		ref sbyte mapRef = ref MemoryMarshal.GetReference(Base58DecodingMap);
		ref char srcRef = ref MemoryMarshal.GetReference(source);
		int length = Math.Min(_Base58Size, source.Length);

		for (int i = 0; i < length; i++) {
			ulong c;

			var x = Unsafe.Add(ref mapRef, (byte)Unsafe.Add(ref srcRef, i));
			if (x < 0) {
				_ReadBase58CharsFailure = (ReadBase58CharsFailCode.InvalidSymbol, i, default);
				return null;
			}

			Debug.Assert(x < 58);
			c = (ulong)u0 * 58 + (ulong)x;
			u0 = (uint)c;
			c >>= 32; // carry

			c = (ulong)u1 * 58 + c;
			u1 = (uint)c;
			c >>= 32;

			c = (ulong)u2 * 58 + c;
			u2 = (uint)c;
			c >>= 32;

			c = (ulong)u3 * 58 + c;
			u3 = (uint)c;
			c >>= 32;

			if (c != 0) {
				_ReadBase58CharsFailure = (ReadBase58CharsFailCode.OverflowCarry, i, c);
				return null;
			}
		}

		return new UniqueId(u3, u2, u1, u0);
	}

	private enum ReadBase58CharsFailCode {
		NA = 0,
		InvalidSymbol = 1,
		OverflowCarry = 2,
	}

	[ThreadStatic]
	private static (ReadBase58CharsFailCode code, int index, ulong carry) _ReadBase58CharsFailure;

	private static void ResetReadBase58CharsFailure() => _ReadBase58CharsFailure = default;

	private static Exception FE_ReadBase58CharsFailure() {
		var (code, i, c) = _ReadBase58CharsFailure;
		return code switch {
			ReadBase58CharsFailCode.InvalidSymbol => new FormatException($"Invalid symbol at index {i}"),
			ReadBase58CharsFailCode.OverflowCarry => new OverflowException(
				$"Accumulated value of base 58 input is too high.{Environment.NewLine}" +
				$"Parsing halted at index {i}, with overflow carry: {c} (0x{c:X})"
			),
			_ => new FormatException(),
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static UniqueId? ReadBase58Chars(ReadOnlySpan<char> source)
		=> ReadBase58Chars_Inline(source);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId? ParseExactOrNull(ReadOnlySpan<char> input) {
		if (_Base58Size != input.Length) {
			return null;
		}
		var result = ReadBase58Chars(input);
		if (result is null) ResetReadBase58CharsFailure();
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId? ParseOrNull(ReadOnlySpan<char> input) {
		var result = ReadBase58Chars(input.Trim());
		if (result is null) ResetReadBase58CharsFailure();
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId ParseExact(ReadOnlySpan<char> input) {
		if (_Base58Size != input.Length) {
			throw AOORE_NeedsExactlyBase58Size(nameof(input));
		}
		return ReadBase58Chars_Inline(input)
			?? throw FE_ReadBase58CharsFailure();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Parse(ReadOnlySpan<char> input) {
		return ReadBase58Chars(input.Trim())
			?? throw FE_ReadBase58CharsFailure();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryParseExact(ReadOnlySpan<char> input, [NotNullWhen(true)] out UniqueId? result)
		=> (result = ParseExactOrNull(input)) is not null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out UniqueId? result)
		=> (result = ParseOrNull(input)) is not null;

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as UniqueId);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public bool Equals([NotNullWhen(true)] UniqueId? other) {
		if (other is null) return false;
		if (ReferenceEquals(this, other)) return true;
		if (GetType() != other.GetType()) return false;

		return _Data.High == other._Data.High
			&& _Data.Low == other._Data.Low;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override int GetHashCode() => HashCode.Combine(_Data.High, _Data.Low);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	int IComparable.CompareTo(object? obj) {
		if (obj is UniqueId other) {
			return CompareTo(other);
		}
		return obj is null ? 1
			: throw new ArgumentException($"Type should be `{nameof(UniqueId)}`", nameof(obj));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public int CompareTo(UniqueId? other) {
		if (other is null) return 1;

		int c = _Data.High.CompareTo(other._Data.High);
		if (c != 0) return c;

		return _Data.Low.CompareTo(other._Data.Low);
	}

	public static bool operator ==(UniqueId? left, UniqueId? right) {
		return left is null ? right is null : left.Equals(right);
	}

	public static bool operator !=(UniqueId? left, UniqueId? right) {
		return !(left == right);
	}

	public static bool operator <(UniqueId? left, UniqueId? right) {
		return left is null ? right is not null : left.CompareTo(right) < 0;
	}

	public static bool operator <=(UniqueId? left, UniqueId? right) {
		return left is null || left.CompareTo(right) <= 0;
	}

	public static bool operator >(UniqueId? left, UniqueId? right) {
		return left is not null && left.CompareTo(right) > 0;
	}

	public static bool operator >=(UniqueId? left, UniqueId? right) {
		return left is null ? right is null : left.CompareTo(right) >= 0;
	}

	// --

	private static ArgumentOutOfRangeException AOORE_NeedsAtLeastBase58Size(string? paramName)
		=> new(paramName, $"Length must be at least {_Base58Size} (or `{nameof(UniqueId)}.{nameof(Base58Size)}`){Environment.NewLine}");

	private static ArgumentOutOfRangeException AOORE_NeedsExactlyBase58Size(string? paramName)
		=> new(paramName, $"Length must be exactly {_Base58Size} (or `{nameof(UniqueId)}.{nameof(Base58Size)}`){Environment.NewLine}");


	private static ArgumentOutOfRangeException AOORE_NeedsAtLeastDataSize(string? paramName)
		=> new(paramName, $"Length must be at least {Data.Size} (or `{nameof(UniqueId)}.{nameof(DataSize)}`){Environment.NewLine}");

	private static ArgumentOutOfRangeException AOORE_NeedsExactlyDataSize(string? paramName)
		=> new(paramName, $"Length must be exactly {Data.Size} (or `{nameof(UniqueId)}.{nameof(DataSize)}`){Environment.NewLine}");
}
