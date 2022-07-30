namespace Kokoro.Internal;

partial class FieldedEntity {
	/// <summary>
	/// The set of classes held by the <see cref="FieldedEntity">fielded entity</see>.
	/// </summary>
	private Classes? _Classes;

	private sealed class Classes : HashSet<long> {
		/// <summary>
		/// The set of classes marked as changed, a.k.a., class change set.
		/// </summary>
		/// <remarks>
		/// RULES:
		/// <br/>- If a class <b>is held</b> while marked as <b>changed</b>, the
		/// "changed" mark should be interpreted as the class awaiting <b>addition</b>
		/// to the new schema of the <see cref="FieldedEntity">fielded entity</see>.
		/// <br/>- If a class <b>is not held</b> while marked as <b>changed</b>,
		/// the "changed" mark should be interpreted as the class awaiting <b>removal</b>
		/// from the new schema, given that it existed in the old schema.
		/// </remarks>
		internal ClassChanges? _Changes;
	}

	private sealed class ClassChanges : HashSet<long> { }
}
