using Kokoro.Test.Framework;

[assembly: TestFramework(TestFramework.TypeName, TestFramework.AssemblyName)]
namespace Kokoro.Test.Framework;

partial class TestFramework {

	public const string TypeNamespace = $"Kokoro.Test.Framework";

	public const string TypeName = $"{TypeNamespace}.{nameof(TestFramework)}";

	public const string AssemblyName = ThisAssembly.Project.AssemblyName;
}
