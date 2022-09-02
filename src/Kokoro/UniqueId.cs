namespace Kokoro;
using Kokoro.Common.Util;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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

		public ReadOnlySpan<byte> RawSpan {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<ByteData, byte>(in this, 0), _Length);
		}

		public byte this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) E_IOOR();
				return UnsafeElementRef<ByteData, byte>(in this, _End-index);
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in ByteData data)
			=> U.As<ByteData, UniqueId>(ref U.AsRef(in data));

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

		/// <summary>
		/// <b>Warning:</b> The endianness of each <see langword="uint"/>
		/// element in the returned <see cref="ReadOnlySpan{uint}">span</see>
		/// is system-dependent.
		/// </summary>
		public ReadOnlySpan<uint> RawSpan {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<UInt32Data, uint>(in this, 0), _Length);
		}

		public uint this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) E_IOOR();
				uint element = UnsafeElementRef<UInt32Data, uint>(in this, _End-index);
				return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(element) : element;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in UInt32Data data)
			=> U.As<UInt32Data, UniqueId>(ref U.AsRef(in data));

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

		/// <summary>
		/// <b>Warning:</b> The endianness of each <see langword="ulong"/>
		/// element in the returned <see cref="ReadOnlySpan{ulong}">span</see>
		/// is system-dependent.
		/// </summary>
		public ReadOnlySpan<ulong> RawSpan {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get => MemoryMarshal.CreateReadOnlySpan(ref UnsafeElementRef<UInt64Data, ulong>(in this, 0), _Length);
		}

		public ulong this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			get {
				if ((uint)index >= _Length) E_IOOR();
				ulong element = UnsafeElementRef<UInt64Data, ulong>(in this, _End-index);
				return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(element) : element;
			}
		}

		public ulong HighBits => this[1];

		public ulong LowBits => this[0];


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UniqueId(in UInt64Data data)
			=> U.As<UInt64Data, UniqueId>(ref U.AsRef(in data));

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
	[DoesNotReturn]
	private static void E_IOOR() => throw new IndexOutOfRangeException();

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

	#region Constructors

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public UniqueId() => this = default;

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public UniqueId(ReadOnlySpan<byte> bytes) {
		if (_Size <= bytes.Length) {
			this = U.As<byte, UniqueId>(ref MemoryMarshal.GetReference(bytes));
		} else {
			UniqueId uid = default;
			bytes.CopyTo(MemoryMarshal.CreateSpan(ref UnsafeElementRef<UniqueId, byte>(in uid, _Size - bytes.Length), bytes.Length));
			this = uid;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public UniqueId(ulong highBits, ulong lowBits) {
		if (BitConverter.IsLittleEndian) {
			UnsafeElementRef<UniqueId, ulong>(in this, 0) = BinaryPrimitives.ReverseEndianness(highBits);
			UnsafeElementRef<UniqueId, ulong>(in this, 1) = BinaryPrimitives.ReverseEndianness(lowBits);
		} else {
			UnsafeElementRef<UniqueId, ulong>(in this, 0) = highBits;
			UnsafeElementRef<UniqueId, ulong>(in this, 1) = lowBits;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public UniqueId(uint u3, uint u2, uint u1, uint u0) {
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

	// TODO Use either `unscoped` or the attribute equivalent once available, so
	// that we can upgrade to C# 11 or beyond.
	// - See, https://github.com/dotnet/runtime/issues/72074
	// - See also, https://github.com/dotnet/csharplang/blob/main/proposals/low-level-struct-improvements.md#violating-scoped
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static ref TElement UnsafeElementRef<TFrom, TElement>(in TFrom from, int elementOffset) {
		return ref U.Add(ref U.As<TFrom, TElement>(ref U.AsRef(in from)), elementOffset);
	}

	/// <summary>
	/// Writable span, for initialization purposes only.
	/// </summary>
	private Span<byte> InitSpanBytes {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => MemoryMarshal.CreateSpan(ref UnsafeElementRef<UniqueId, byte>(in this, 0), _Size);
	}

	#region `Create()` methods

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static UniqueId Create() {
		UniqueId uid;
		RandomNumberGenerator.Fill(uid.InitSpanBytes);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(ReadOnlySpan<byte> bytes) => new(bytes); // Alias

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static UniqueId CreateStrict(ReadOnlySpan<byte> bytes)
		=> MemoryMarshal.AsRef<UniqueId>(bytes); // May throw

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static UniqueId Create<TState>(TState state, SpanAction<byte, TState> action) {
		UniqueId uid = new();
		action(uid.InitSpanBytes, state);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static UniqueId CreateFast<TState>(TState state, SpanAction<byte, TState> action) {
		UniqueId uid;
		action(uid.InitSpanBytes, state);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static UniqueId Create(Guid guid) {
		UniqueId uid;
		guid.TryWriteUuidBytes(uid.InitSpanBytes);
		return uid;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(ulong highBits, ulong lowBits) => new(highBits, lowBits); // Alias

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(uint u3, uint u2, uint u1, uint u0) => new(u3, u2, u1, u0); // Alias

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId Create(string base58Str) => ParseExact(base58Str); // Alias

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UniqueId CreateLenient(string base58Str) => Parse(base58Str); // Alias

	#endregion

	public static UniqueId Empty => default;

	public bool IsEmpty {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			// Ternary operator returning true/false prevents redundant asm generation:
			// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
			return HighBits == 0 && LowBits == 0 ? true : false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] ToByteArray() => Span.ToArray();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBytes(Span<byte> destination) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return Span.TryCopyTo(destination) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Let JIT decide whether to inline
	public void WriteBytes(Span<byte> destination) => Span.CopyTo(destination); // May throw

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] WriteBytes(byte[] destination) {
		WriteBytes(destination.AsSpan());
		return destination;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Guid ToGuid() => UuidUtils.GuidFromUuid(Span);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string ToHexString() {
		// See,
		// - https://stackoverflow.com/a/10048895
		// - https://stackoverflow.com/a/71904920
		return Convert.ToHexString(Span);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override string ToString() => ToBase58String(); // Alias

	#region Base58 Conversions

	public static int Base58Size => _Base58Size;

	// Allocate enough space in big-endian Base58 representation
	// - See, https://github.com/bitcoin/bitcoin/blob/1e7564eca8a688f39c75540877ec3bdfdde766b1/src/base58.cpp#L96
	// - Equiv. to, ceil(N ceil(log(256) / log(58), 0.0001))
	private const int _Base58Size = (_Size * 13657 - 1) / 10000 + 1;

	private static ReadOnlySpan<byte> Base58EncodingMap => new byte[58] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295
		(byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8',
		(byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G',
		(byte)'H', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N', (byte)'P', (byte)'Q',
		(byte)'R', (byte)'S', (byte)'T', (byte)'U', (byte)'V', (byte)'W', (byte)'X', (byte)'Y',
		(byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f', (byte)'g',
		(byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'m', (byte)'n', (byte)'o', (byte)'p',
		(byte)'q', (byte)'r', (byte)'s', (byte)'t', (byte)'u', (byte)'v', (byte)'w', (byte)'x',
		(byte)'y', (byte)'z',
	};

	private static ReadOnlySpan<sbyte> Base58DecodingMap => new sbyte[256] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295
		// - See also, https://github.com/dotnet/runtime/blob/v6.0.3/src/libraries/System.Private.CoreLib/src/System/Convert.Base64.cs#L195
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

	#region To Base58

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UnsafeWriteBase58Chars(Span<char> destination) {
		Debug.Assert(_Base58Size <= destination.Length);
		UnsafeWriteBase58Chars(ref MemoryMarshal.GetReference(destination));
	}

	/// <summary>
	/// Strategy: Treats the underlying data as a 128-bit unsigned integer then converts it to a Base58 integer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private void UnsafeWriteBase58Chars(ref char destRef) {
		uint u3 = _UInt32s[3], u2 = _UInt32s[2], u1 = _UInt32s[1], u0 = _UInt32s[0];

		// Get a reference to avoid unnecessary range checking
		ref byte mapRef = ref MemoryMarshal.GetReference(Base58EncodingMap);

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

			U.Add(ref destRef, i) = (char)U.Add(ref mapRef, (int)c);
		}

		Debug.Assert(u3 == 0, $"{nameof(u3)}: {u3} (0x{u3:X})");
		Debug.Assert(u2 == 0, $"{nameof(u2)}: {u2} (0x{u2:X})");
		Debug.Assert(u1 == 0, $"{nameof(u1)}: {u1} (0x{u1:X})");
		Debug.Assert(u0 == 0, $"{nameof(u0)}: {u0} (0x{u0:X})");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteBase58Chars(Span<char> destination) {
		if (_Base58Size <= destination.Length) {
			UnsafeWriteBase58Chars(destination);
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteBase58Chars(Span<char> destination) {
		if (_Base58Size > destination.Length) {
			WriteBase58Chars__E_DestinationTooShort_AOOR_destination();
		}
		UnsafeWriteBase58Chars(destination);
	}

	[DoesNotReturn]
	private static void WriteBase58Chars__E_DestinationTooShort_AOOR_destination()
		=> throw Ex_DestinationTooShort_AOOR_destination();

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public string ToBase58String() {
		// Equiv. to `string.Create<TState>(…)` without having to allocate a `SpanAction`
		string str = new('\0', _Base58Size);
		UnsafeWriteBase58Chars(ref MemoryMarshal.GetReference(str.AsSpan()));
		// Given how strings are represented in memory, and that we're creating
		// via `new string()`, it's unlikely that the above trick will break
		// something. That is, it should be guaranteed that we'll get a
		// different reference (or `ref char` location) every time.
		return str;
	}

	#endregion

	#region From Base58

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static bool TryParse(ReadOnlySpan<char> input, out UniqueId result) {
		uint u3 = 0, u2 = 0, u1 = 0, u0 = 0;

		// Get references to avoid unnecessary range checking
		ref sbyte mapRef = ref MemoryMarshal.GetReference(Base58DecodingMap);
		ref char srcRef = ref MemoryMarshal.GetReference(input);
		int length = Math.Min(_Base58Size, input.Length);

		ulong c;
		int i = 0;
	Loop:
		for (; i < length; i++) {
			sbyte x = U.Add(ref mapRef, (byte)U.Add(ref srcRef, i));
			if (x < 0) goto Fail_InvalidSymbol;

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

			if (c != 0) goto Fail_OverflowCarry;
		}

	Success:
		result = new(u3, u2, u1, u0);
		return true;

	Fail_InvalidSymbol:
		{
			int s = i;
			do {
				if (!char.IsWhiteSpace(U.Add(ref srcRef, i))) {
					goto MaybeFail;
				}
			} while (++i < length);

			goto Success;

		MaybeFail:
			if (i > 0 && s == 0) {
				// White space simply trimmed from start
				goto Loop; // Try again
			}

			ref var fail = ref ParseFail.Current;
			fail.Code = ParseFailCode.InvalidSymbol;
			fail.Index = i;
			goto Fail;
		}

	Fail:
		result = default;
		return false;

	Fail_OverflowCarry:
		{
			ref var fail = ref ParseFail.Current;
			fail.Code = ParseFailCode.OverflowCarry;
			fail.Index = i;
			fail.Carry = c;
			goto Fail;
		}
	}

	private enum ParseFailCode : byte {
		NA = 0,
		InvalidSymbol = 1,
		OverflowCarry = 2,
	}

	[StructLayout(LayoutKind.Auto)]
	private struct ParseFail {
		public ParseFailCode Code;
		public int Index;
		public ulong Carry;

		[ThreadStatic]
		public static ParseFail Current;

		public static Exception ConsumeException() {
			var current = Current;
			Current = default;

			switch (current.Code) {
				case ParseFailCode.InvalidSymbol: {
					return new FormatException($"Invalid symbol at index {current.Index}");
				}
				case ParseFailCode.OverflowCarry: {
					return new OverflowException(
						$"Accumulated value of Base58 input is too high.{Environment.NewLine}" +
						$"Parsing halted at index {current.Index}, with overflow carry: {current.Carry} (0x{current.Carry:X})"
					);
				}
				default: {
					Trace.Fail("Unexpected exception path");
					return new FormatException("NA");
				}
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool TryParseExact(ReadOnlySpan<char> input, out UniqueId result) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return _Base58Size != input.Length || !TryParse(input, out result) ? false : true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static UniqueId Parse(ReadOnlySpan<char> input) {
		if (!TryParse(input, out var result)) {
			Parse__E_Fail();
		}
		return result;
	}

	[DoesNotReturn]
	private static void Parse__E_Fail() => throw ParseFail.ConsumeException();

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public static UniqueId ParseExact(ReadOnlySpan<char> input) {
		if (_Base58Size != input.Length) {
			ParseExact__E_LengthNotExact_AOOR_input();
		}
		if (!TryParse(input, out var result)) {
			ParseExact__E_Fail();
		}
		return result;
	}

	[DoesNotReturn]
	private static void ParseExact__E_LengthNotExact_AOOR_input()
		=> throw new ArgumentOutOfRangeException(paramName: "input", $"Input span needs to be exactly {_Base58Size} in length.");

	[DoesNotReturn]
	private static void ParseExact__E_Fail() => throw ParseFail.ConsumeException();

	#endregion

	#endregion

	#region Clockwork Base32 Conversions

	// Clockwork Base32 is a simple variant of Base32 inspired by Crockford's Base32.
	// - Official spec at, https://gist.github.com/szktty/228f85794e4187882a77734c89c384a8

	public static int CwBase32Size => _CwBase32Size;

	// Equiv. to, ceil(N * 8 / 5)
	private const int _CwBase32Size = (_Size * 8 - 1) / 5 + 1;

	private static ReadOnlySpan<byte> CwBase32EncodingMap => new byte[32] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295
		(byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
		(byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F',
		(byte)'G', (byte)'H', (byte)'J', (byte)'K', (byte)'M', (byte)'N', (byte)'P', (byte)'Q',
		(byte)'R', (byte)'S', (byte)'T', (byte)'V', (byte)'W', (byte)'X', (byte)'Y', (byte)'Z',
	};

	private static ReadOnlySpan<sbyte> CwBase32DecodingMap => new sbyte[256] {
		// Relies on C# compiler optimization to reference static data
		// - See, https://github.com/dotnet/csharplang/issues/5295
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		 0, 1, 2, 3, 4, 5, 6, 7,  8, 9, 0,-1,-1,-1,-1,-1,
		-1,10,11,12,13,14,15,16, 17, 1,18,19, 1,20,21, 0,
		22,23,24,25,26,-2,27,28, 29,30,31,-1,-1,-1,-1,-1,
		-1,10,11,12,13,14,15,16, 17, 1,18,19, 1,20,21, 0,
		22,23,24,25,26,-1,27,28, 29,30,31,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
		-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	};

	// TODO Implement. Needed for creating unique filenames.

	#region To Clockwork Base32

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UnsafeWriteCwBase32Chars(Span<char> destination) {
		Debug.Assert(_CwBase32Size <= destination.Length);
		UnsafeWriteCwBase32Chars(ref MemoryMarshal.GetReference(destination));
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private void UnsafeWriteCwBase32Chars(ref char destRef) {
		// Get a reference to avoid unnecessary range checking
		ref byte mapRef = ref MemoryMarshal.GetReference(CwBase32EncodingMap);

		ulong bits = LowBits;

		Debug.Assert(_CwBase32Size == 26);
		U.Add(ref destRef, 25) = (char)U.Add(ref mapRef, ((nuint)bits << 2) & 0x1F);
		U.Add(ref destRef, 24) = (char)U.Add(ref mapRef, (nuint)(bits >>= 3) & 0x1F);

		U.Add(ref destRef, 23) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 22) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 21) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 20) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 19) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 18) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 17) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 16) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 15) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 14) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 13) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);

		nuint leftover = (nuint)bits >> 5;
		Debug.Assert(leftover == (leftover & 0x1)); // Expect only 1 bit left

		bits = HighBits;

		U.Add(ref destRef, 12) = (char)U.Add(ref mapRef, (((nuint)bits << 1) | leftover) & 0x1F);
		U.Add(ref destRef, 11) = (char)U.Add(ref mapRef, (nuint)(bits >>= 4) & 0x1F);
		U.Add(ref destRef, 10) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);

		U.Add(ref destRef, 9) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 8) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 7) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 6) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 5) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 4) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 3) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 2) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 1) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);
		U.Add(ref destRef, 0) = (char)U.Add(ref mapRef, (nuint)(bits >>= 5) & 0x1F);

		Debug.Assert(bits >> 5 == 0); // Expect no bits left
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryWriteCwBase32Chars(Span<char> destination) {
		if (_CwBase32Size <= destination.Length) {
			UnsafeWriteCwBase32Chars(destination);
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteCwBase32Chars(Span<char> destination) {
		if (_CwBase32Size > destination.Length) {
			WriteCwBase32Chars__E_DestinationTooShort_AOOR_destination();
		}
		UnsafeWriteCwBase32Chars(destination);
	}

	[DoesNotReturn]
	private static void WriteCwBase32Chars__E_DestinationTooShort_AOOR_destination()
		=> throw Ex_DestinationTooShort_AOOR_destination();

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public string ToCwBase32String() {
		// Equiv. to `string.Create<TState>(…)` without having to allocate a `SpanAction`
		string str = new('\0', _CwBase32Size);
		UnsafeWriteCwBase32Chars(ref MemoryMarshal.GetReference(str.AsSpan()));
		// Given how strings are represented in memory, and that we're creating
		// via `new string()`, it's unlikely that the above trick will break
		// something. That is, it should be guaranteed that we'll get a
		// different reference (or `ref char` location) every time.
		return str;
	}

	#endregion

	#region From Clockwork Base32

	#endregion

	#endregion

	#region Equality and Comparability

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals([NotNullWhen(true)] object? obj) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return obj is UniqueId other && Equals(other) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(UniqueId uid) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return HighBits == uid.HighBits && LowBits == uid.LowBits ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public override int GetHashCode() => HashCode.Combine(HighBits, LowBits);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(object? obj) {
		if (obj is UniqueId uid) return CompareTo(uid);
		if (obj != null) CompareTo__E_IncompatibleType_Arg();
		return 1;
	}

	[DoesNotReturn]
	private static void CompareTo__E_IncompatibleType_Arg()
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
	public static bool operator ==(UniqueId left, UniqueId right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return left.Equals(right) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(UniqueId left, UniqueId right) {
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		return !left.Equals(right) ? true : false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a < b ? true : false;
			}
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a < b ? true : false;
			}
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a < b ? true : false;
			}
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a < b ? true : false;
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a > b ? true : false;
			}
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a > b ? true : false;
			}
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(UniqueId left, UniqueId right) {
		// Compare high-order bits
		{
			ulong a = left.HighBits;
			ulong b = right.HighBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a > b ? true : false;
			}
		}
		// Compare low-order bits
		{
			ulong a = left.LowBits;
			ulong b = right.LowBits;

			if (a != b) {
				// Ternary operator returning true/false prevents redundant asm generation:
				// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
				return a > b ? true : false;
			}
		}
		return true;
	}

	#endregion

	#endregion

	#region Common Exceptions

	private static ArgumentOutOfRangeException Ex_DestinationTooShort_AOOR_destination()
		=> new(paramName: "destination", "Destination is too short.");

	#endregion
}
