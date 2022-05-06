namespace Kokoro.Common.Caching;
using System.Collections.Generic;

internal class LruCache<TKey, TValue> where TKey : notnull {
	private readonly Dictionary<TKey, Node> _Map;

	private Node? _Head; // The most recently accessed node
	private Node? _Tail; // The least recently accessed node

	// TODO Can be optimized by using a struct for the nodes, and an array of
	// such structs, where the next and previous pointers are 32-bit integers,
	// and perhaps -1 represents the null pointer.
	internal sealed class Node {
		internal Node? Next;
		internal Node? Prev;

		internal TKey Key;
		internal TValue Value;

		internal int Size;

		public Node(TKey key, TValue value, int size) {
			Key = key;
			Value = value;
			Size = size;
		}
	}

	private int _Size;
	private readonly int _MaxSize;

	/// <summary>
	/// Size of this cache in units. Not necessarily the number of elements.
	/// </summary>
	public int Size => _Size;

	public int MaxSize => _MaxSize;

	public int MapCount => _Map.Count;


	public LruCache(int maxSize) : this(maxSize, 0) { }

	public LruCache(int maxSize, int capacity) {
		_MaxSize = maxSize;
		_Map = new(capacity);
	}


	public void EnsureMapCapacity(int capacity) => _Map.EnsureCapacity(capacity);

	public void TrimMapExcess() => _Map.TrimExcess();


	protected virtual int SizeOf(TKey key, TValue value) => 1;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int SafeSizeOf(TKey key, TValue value) {
		int size = SizeOf(key, value);
		if (size < 1) SizeOf__E_CannotBeLT1_InvOp(key, value, size);
		return size;
	}

	[DoesNotReturn]
	private static void SizeOf__E_CannotBeLT1_InvOp(TKey key, TValue value, int resultSize)
		=> throw new InvalidOperationException($"Size less than 1 ({resultSize}): [{key}]={value}");


	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
		if (!_Map.TryGetValue(key, out var node)) {
			value = default;
			return false;
		}

		// Detach node
		{
			var prev = node.Prev;
			if (prev == null) {
				// NOTE: The node is currently the head, since only the head can
				// have a null previous node.
				goto Done;
			}

			var next = node.Next;
			prev.Next = next;

			if (next != null) {
				// NOTE: The node isn't the tail, since only the tail can have a
				// null next node.
				next.Prev = prev;
			}
		}

		// Reattach node (as the new head)
		{
			node.Prev = null; // Clear since the node will be the new head

			var next = _Head;
			node.Next = next;

			Debug.Assert(next != null, "Internal map isn't empty but the head node is null");
			next.Prev = node;
			_Head = node;
		}

	Done:
		value = node.Value;
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void Add(TKey key, TValue value) {
		int entrySize = SafeSizeOf(key, value);
		Node node = new(key, value, entrySize);

		var map = _Map;
		map.Add(key, node); // TODO Allow replacement of existing entry (instead of throwing)

		int size = _Size += entrySize;
		int maxSize = _MaxSize;

		// Trim to max size
		if (size > maxSize && size > entrySize) {
			var prev = _Tail;
			Debug.Assert(prev != null, "Size isn't zero but the tail node is null");
			for (; ; ) {
				if (!map.Remove(prev.Key)) Debug.Fail($"Linked node apparently not in map: [{prev.Key}]={prev.Value}");
				size -= prev.Size;
				prev = prev.Prev;

				if (prev != null) {
					if (size > maxSize) {
						continue;
					}
					// Done! Size is now under maximum.
					prev.Next = null; // Cut the link to the removed nodes
					_Tail = prev; // This node is now the oldest node
					break;
				} else {
					// Head node reached! We're now completely empty.
					_Head = null;
					// ^ Signals that we're now completely empty and that the
					// new node will be the first entry. The new node will also
					// be the tail, which we'll set later below.
					break;
				}
			}
			_Size = size;
		}

		// Attach new node (as the new head)
		{
			var next = _Head;
			node.Next = next;

			if (next != null) {
				next.Prev = node;
			} else {
				// NOTE: If the head is null, then we're currently adding the
				// first node. We must also therefore set it as the tail.
				_Tail = node;
			}

			_Head = node; // Set as the newest node
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool Remove(TKey key) {
		if (_Map.Remove(key, out var node)) {
			// Detach node

			var prev = node.Prev;
			if (prev != null) {
				var next = node.Next;
				prev.Next = next;

				if (next != null) {
					next.Prev = prev;
				} else {
					// NOTE: The node was the tail, since only the tail can have
					// a null next node.
					_Tail = prev;
				}
			} else {
				// NOTE: The node was the head, since only the head can have a
				// null previous node.
				var next = node.Next;
				_Head = next;

				if (next != null) {
					next.Prev = null;
				} else {
					// NOTE: The node was also the tail, since only the tail can
					// have a null next node.
					_Tail = null;
				}
			}

			return true;
		}
		return false;
	}

	public void Clear() {
		_Map.Clear();

		// See also, https://blog.stephencleary.com/2010/02/q-should-i-set-variables-to-null-to.html
		_Head = _Tail = null;

		_Size = 0;
	}
}
