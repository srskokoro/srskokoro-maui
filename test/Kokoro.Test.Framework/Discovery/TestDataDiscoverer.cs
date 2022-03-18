namespace Kokoro.Test.Framework.Discovery;
using Xunit.Sdk;

public class TestDataDiscoverer : DataDiscoverer {

	public const string TypeNamespace = $"Kokoro.Test.Framework.Discovery";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestDataDiscoverer)}";

	/// <inheritdoc/>
	public override bool SupportsDiscoveryEnumeration(IAttributeInfo dataAttribute, IMethodInfo testMethod) {
		return !dataAttribute.GetNamedArgument<bool>("DisableDiscoveryEnumeration");
	}
}
