﻿namespace Kokoro.Internal.Marshal.Fields;
using System.IO;

internal sealed class ColdFieldsReader : BaseFieldsReader {

	public ColdFieldsReader(Stream stream) : base(stream) { }
}
