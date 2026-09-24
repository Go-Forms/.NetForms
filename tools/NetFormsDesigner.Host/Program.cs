using System;
using System.IO;
using System.Text;
using NetForms.Design;

// One JSON request per line on stdin, one JSON response per line on stdout (DesignerProtocol).
// Anything else the process might print - a Console.WriteLine in some control - goes to stderr,
// so it can never corrupt the protocol stream.
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.InputEncoding = utf8;
var protocolOut = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
Console.SetOut(Console.Error);

using var protocol = new DesignerProtocol();
string? line;
while ((line = Console.In.ReadLine()) != null)
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    protocolOut.WriteLine(protocol.Handle(line));
    if (protocol.Closed) break;
}
