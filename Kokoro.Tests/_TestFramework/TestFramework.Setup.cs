using Kokoro.TestFramework;

[assembly: TestFramework(TestFramework.TypeName, TestFramework.AssemblyName)]
namespace Kokoro.TestFramework;

partial class TestFramework {

	public const string TypeNamespace = $"Kokoro.TestFramework";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestFramework)}";

	public const string AssemblyName = $"Kokoro.Tests";
}
