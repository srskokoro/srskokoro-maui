using Kokoro.Test.Framework;

[assembly: TestFramework(TestFramework.TypeName, ThisAssembly.Name)]
namespace Kokoro.Test.Framework;

partial class TestFramework {

	public const string TypeNamespace = $"Kokoro.Test.Framework";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestFramework)}";
}
