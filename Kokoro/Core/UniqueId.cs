﻿using Kokoro.Util;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Kokoro;

[StructLayout(LayoutKind.Explicit, Size = _Size)]
public readonly struct UniqueId : IEquatable<UniqueId>, IComparable, IComparable<UniqueId> {
	private const int _Size = 16;

	[FieldOffset(0)] readonly ByteData _Bytes;
	[FieldOffset(0)] readonly UInt32Data _UInt32s;
	[FieldOffset(0)] readonly UInt64Data _UInt64s;

	#region XxxxData type definitions

	[StructLayout(LayoutKind.Explicit, Size = _Size)]
	public readonly struct ByteData {
		private const int _Length = _Size;
		private const int _End = _Length-1;

		public ReadOnlySpan<byte> Span {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<ByteData, byte>(in this, 0), _Length);
		}

		public byte this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) Throw_IOORE();
				byte element = UnsafeElementRef<ByteData, byte>(in this, _End-index);
				return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(element) : element;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in ByteData data)
			=> Unsafe.As<ByteData, UniqueId>(ref Unsafe.AsRef(in data));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ByteData(in UniqueId uid)
			=> uid._Bytes;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ByteData(in UInt32Data data)
			=> ((UniqueId)data)._Bytes;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ByteData(in UInt64Data data)
			=> ((UniqueId)data)._Bytes;
	}

	[StructLayout(LayoutKind.Explicit, Size = _Size)]
	public readonly struct UInt32Data {
		private const int _Length = _Size/sizeof(uint);
		private const int _End = _Length-1;

		public ReadOnlySpan<uint> Span {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<UInt32Data, uint>(in this, 0), _Length);
		}

		public uint this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) Throw_IOORE();
				uint element = UnsafeElementRef<UInt32Data, uint>(in this, _End-index);
				return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(element) : element;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in UInt32Data data)
			=> Unsafe.As<UInt32Data, UniqueId>(ref Unsafe.AsRef(in data));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt32Data(in UniqueId uid)
			=> uid._UInt32s;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt32Data(in ByteData data)
			=> ((UniqueId)data)._UInt32s;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt32Data(in UInt64Data data)
			=> ((UniqueId)data)._UInt32s;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt32Data((uint u3, uint u2, uint u1, uint u0) bitsQuad)
			=> new UniqueId(bitsQuad.u3, bitsQuad.u2, bitsQuad.u1, bitsQuad.u0);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (uint u3, uint u2, uint u1, uint u0)(UInt32Data uint32s)
			=> (uint32s[3], uint32s[2], uint32s[1], uint32s[0]);

		public (uint u3, uint u2, uint u1, uint u0) Tuple => this;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint u3, out uint u2, out uint u1, out uint u0) {
			u3 = this[3]; u2 = this[2]; u1 = this[1]; u0 = this[0];
		}
	}

	[StructLayout(LayoutKind.Explicit, Size = _Size)]
	public readonly struct UInt64Data {
		private const int _Length = _Size/sizeof(ulong);
		private const int _End = _Length-1;

		public ReadOnlySpan<ulong> Span {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<UInt64Data, ulong>(in this, 0), _Length);
		}

		public ulong this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) Throw_IOORE();
				ulong element = UnsafeElementRef<UInt64Data, ulong>(in this, _End-index);
				return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(element) : element;
			}
		}

		public ulong HighBits => this[1];

		public ulong LowBits => this[0];


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in UInt64Data data)
			=> Unsafe.As<UInt64Data, UniqueId>(ref Unsafe.AsRef(in data));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt64Data(in UniqueId uid)
			=> uid._UInt64s;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt64Data(in ByteData data)
			=> ((UniqueId)data)._UInt64s;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt64Data(in UInt32Data data)
			=> ((UniqueId)data)._UInt64s;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UInt64Data((ulong highBits, ulong lowBits) bitsPair)
			=> new UniqueId(bitsPair.highBits, bitsPair.lowBits);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (ulong highBits, ulong lowBits)(UInt64Data uint64s)
			=> (uint64s[1], uint64s[0]);

		public (ulong highBits, ulong lowBits) Tuple => this;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ulong highBits, out ulong lowBits) {
			highBits = this[1]; lowBits = this[0];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out ulong highBits, out ulong lowBits) {
		highBits = _UInt64s[1]; lowBits = _UInt64s[0];
	}

	[StackTraceHidden]
	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void Throw_IOORE() => throw new IndexOutOfRangeException();

	#endregion

	public static int Size => _Size;


	public ByteData Bytes => _Bytes;

	public UInt32Data UInt32s => _UInt32s;

	public UInt64Data UInt64s => _UInt64s;


	public ulong HighBits => _UInt64s.HighBits;

	public ulong LowBits => _UInt64s.LowBits;


	public ReadOnlySpan<byte> Span {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<UniqueId, byte>(in this, 0), _Size);
	}

	/// <summary>
	/// Writable span, for initialization purposes only.
	/// </summary>
	private Span<byte> InitSpanBytes {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => MemoryMarshal.CreateSpan(ref UnsafeElementRef<UniqueId, byte>(in this, 0), _Size);
	}

	#region `Create()` methods

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create() {
		UniqueId uid = new();
		RandomNumberGenerator.Fill(uid.InitSpanBytes);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create(ReadOnlySpan<byte> bytes) => new(bytes);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId CreateLenient(ReadOnlySpan<byte> bytes) {
		if (_Size > bytes.Length) {
			UniqueId uid = new();
			bytes.CopyTo(uid.InitSpanBytes[(_Size - bytes.Length)..]);
			return uid;
		}
		return new(bytes);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create<TState>(TState state, SpanAction<byte, TState> action) {
		UniqueId uid = new();
		action(uid.InitSpanBytes, state);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create(Guid guid) {
		UniqueId uid = new();
		guid.TryWriteUuidBytes(uid.InitSpanBytes);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create(ulong highBits, ulong lowBits) => new(highBits, lowBits);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId Create(uint u3, uint u2, uint u1, uint u0) => new(u3, u2, u1, u0);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(string base58Str) => ParseExact(base58Str);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId CreateLenient(string base58Str) => Parse(base58Str);

	#endregion

	private static readonly UniqueId _Empty = new();

	public static UniqueId Empty => _Empty;

	public bool IsEmpty => HighBits == 0 && LowBits == 0;

	#region Constructors

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public UniqueId() => this = default;

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(ReadOnlySpan<byte> bytes) {
		this = MemoryMarshal.AsRef<UniqueId>(bytes); // May throw
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(ulong highBits, ulong lowBits) {
		if (BitConverter.IsLittleEndian) {
			UnsafeElementRef<UniqueId, ulong>(in this, 0) = BinaryPrimitives.ReverseEndianness(highBits);
			UnsafeElementRef<UniqueId, ulong>(in this, 1) = BinaryPrimitives.ReverseEndianness(lowBits);
		} else {
			UnsafeElementRef<UniqueId, ulong>(in this, 0) = highBits;
			UnsafeElementRef<UniqueId, ulong>(in this, 1) = lowBits;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private UniqueId(uint u3, uint u2, uint u1, uint u0) {
		if (BitConverter.IsLittleEndian) {
			UnsafeElementRef<UniqueId, uint>(in this, 0) = BinaryPrimitives.ReverseEndianness(u3);
			UnsafeElementRef<UniqueId, uint>(in this, 1) = BinaryPrimitives.ReverseEndianness(u2);
			UnsafeElementRef<UniqueId, uint>(in this, 2) = BinaryPrimitives.ReverseEndianness(u1);
			UnsafeElementRef<UniqueId, uint>(in this, 3) = BinaryPrimitives.ReverseEndianness(u0);
		} else {
			UnsafeElementRef<UniqueId, uint>(in this, 0) = u3;
			UnsafeElementRef<UniqueId, uint>(in this, 1) = u2;
			UnsafeElementRef<UniqueId, uint>(in this, 2) = u1;
			UnsafeElementRef<UniqueId, uint>(in this, 3) = u0;
		}
	}

	#endregion

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static ref TElement UnsafeElementRef<TFrom, TElement>(in TFrom from, int elementOffset) {
		return ref Unsafe.Add(ref Unsafe.As<TFrom, TElement>(ref Unsafe.AsRef(in from)), elementOffset);
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] ToByteArray() => Span.ToArray();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBytes(Span<byte> destination) => Span.TryCopyTo(destination);

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Let JIT decide whether to inline
	public void WriteBytes(Span<byte> destination) => Span.CopyTo(destination); // May throw

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] WriteBytes(byte[] destination) {
		WriteBytes(destination.AsSpan());
		return destination;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Guid ToGuid() => UuidUtil.GuidFromUuid(Span);

	#region Base 58 Conversions

	public static int Base58Size => _Base58Size;

	// Allocate enough space in big-endian base 58 representation
	// - See, https://github.com/bitcoin/bitcoin/blob/1e7564eca8a688f39c75540877ec3bdfdde766b1/src/base58.cpp#L96
	// - Equiv. to, ceil(N ceil(log(256) / log(58), 0.0001))
	private const int _Base58Size = (_Size * 13657 - 1) / 10000 + 1;

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

	#region To Base 58

	/// <summary>
	/// Strategy: Treats the underlying data as a 128-bit unsigned integer then converts it to a base 58 integer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void UnsafeWriteBase58Chars(Span<char> destination) {
		uint u3 = _UInt32s[3], u2 = _UInt32s[2], u1 = _UInt32s[1], u0 = _UInt32s[0];

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

			Unsafe.Add(ref destRef, i) = Unsafe.Add(ref mapRef, (int)c);
		}

		Debug.Assert(u3 == 0, $"{nameof(u3)}: {u3} (0x{u3:X})", null);
		Debug.Assert(u2 == 0, $"{nameof(u2)}: {u2} (0x{u2:X})", null);
		Debug.Assert(u1 == 0, $"{nameof(u1)}: {u1} (0x{u1:X})", null);
		Debug.Assert(u0 == 0, $"{nameof(u0)}: {u0} (0x{u0:X})", null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBase58Chars(Span<char> destination) {
		if (_Base58Size > destination.Length) {
			return false;
		}
		UnsafeWriteBase58Chars(destination);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteBase58Chars(Span<char> destination) {
		if (_Base58Size > destination.Length) {
			WriteBase58Chars__AOORE_DestinationTooShort(nameof(destination));
		}
		UnsafeWriteBase58Chars(destination);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void WriteBase58Chars__AOORE_DestinationTooShort(string? paramName)
		=> throw new ArgumentOutOfRangeException(paramName, "Destination is too short.");

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override string ToString() {
		string str = new('\0', _Base58Size);
		UnsafeWriteBase58Chars(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(str.AsSpan()), _Base58Size));
		return str;
	}

	#endregion

	#region From Base 58

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static bool TryParse(ReadOnlySpan<char> input, out UniqueId result) {
		uint u3 = 0, u2 = 0, u1 = 0, u0 = 0;
		input = input.Trim();

		// Get references to avoid unnecessary range checking
		ref sbyte mapRef = ref MemoryMarshal.GetReference(Base58DecodingMap);
		ref char srcRef = ref MemoryMarshal.GetReference(input);
		int length = Math.Min(_Base58Size, input.Length);

		for (int i = 0; i < length; i++) {
			ulong c;

			var x = Unsafe.Add(ref mapRef, (byte)Unsafe.Add(ref srcRef, i));
			if (x < 0) {
				ParseFailure.Current = new() {
					Code = ParseFailCode.InvalidSymbol,
					Index = i,
				};
				goto Fail;
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
				ParseFailure.Current = new() {
					Code = ParseFailCode.OverflowCarry,
					Index = i, Carry = c,
				};
				goto Fail;
			}
		}

		result = new(u3, u2, u1, u0);
		return true;

	Fail:
		result = default;
		return false;
	}

	private enum ParseFailCode : byte {
		NA = 0,
		InvalidSymbol = 1,
		OverflowCarry = 2,
	}

	[StructLayout(LayoutKind.Auto)]
	private struct ParseFailure {
		public ParseFailCode Code;
		public int Index;
		public ulong Carry;

		[ThreadStatic]
		public static ParseFailure Current;

		public static Exception ToException() {
			var current = Current;
			return current.Code switch {
				ParseFailCode.InvalidSymbol => new FormatException($"Invalid symbol at index {current.Index}"),
				ParseFailCode.OverflowCarry => new OverflowException(
					$"Accumulated value of base 58 input is too high.{Environment.NewLine}" +
					$"Parsing halted at index {current.Index}, with overflow carry: {current.Carry} (0x{current.Carry:X})"
				),
				_ => new FormatException(),
			};
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool TryParseExact(ReadOnlySpan<char> input, out UniqueId result) {
		if (_Base58Size != input.Length) {
			return false;
		}
		return TryParse(input, out result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Parse(ReadOnlySpan<char> input) {
		if (!TryParse(input, out var result)) {
			Parse__E_Failure();
		}
		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void Parse__E_Failure() => throw ParseFailure.ToException();

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static UniqueId ParseExact(ReadOnlySpan<char> input) {
		if (_Base58Size != input.Length || !TryParse(input, out var result)) {
			ParseExact__E_Failure();
		}
		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private static void ParseExact__E_Failure() => throw ParseFailure.ToException();

	#endregion

	#endregion

	#region Equality and Comparability

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is UniqueId other && Equals(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(UniqueId uid) => HighBits == uid.HighBits && LowBits == uid.LowBits;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(HighBits, LowBits);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(object? obj) {
		if (obj is not UniqueId uid) {
			if (obj is null) return 1;
			CompareTo__AE_IncompatibleType();
		}
		return CompareTo(uid);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[DoesNotReturn]
	private void CompareTo__AE_IncompatibleType()
		=> throw new ArgumentException($"Object must be of type {nameof(UniqueId)}.");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(UniqueId uid) {
		// Compare high-order bits
		{
			ulong a = HighBits;
			ulong b = uid.HighBits;

			if (a != b) return a < b ? -1 : 1;
		}
		// Compare low-order bits
		{
			ulong a = LowBits;
			ulong b = uid.LowBits;

			if (a != b) return a < b ? -1 : 1;
		}
		return 0;
	}

	#region Relational Operators

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(UniqueId left, UniqueId right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(UniqueId left, UniqueId right) => !left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) return a < b;
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) return a < b;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) return a < b;
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) return a < b;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) return a > b;
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) return a > b;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) return a > b;
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) return a > b;
		}
		return true;
	}

	#endregion

	#endregion
}
