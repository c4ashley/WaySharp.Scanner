using System.Text;
using System.Xml;

if (args.Length == 0)
{
	Console.Error.WriteLine("Please specify a Wayland Protocol XML file to parse.");
	return 1;
}

if (!File.Exists(args[0]))
{
	Console.Error.WriteLine("File does not exist");
	return 1;
}

using var fileReader = File.OpenRead(args[0]);
var xml = new XmlDocument();
xml.Load(fileReader);

if (xml.DocumentElement?.Name != "protocol")
	throw new XmlException($"Expecting <protocol> root node but got '{xml.DocumentElement?.Name}' instead.");
XmlElement root = xml.DocumentElement!;
string protocolName = SnakeToPascalCase(root.GetAttribute("name")) ?? throw new XmlException("Root element <protocol> has no 'name' attribute.");
List<InterfaceDefinition> interfaces = [];
foreach (XmlElement child in root)
{
	switch (child.Name)
	{
		case "copyright":
			break;
		case "interface":
		{
			string? description = null, summary = null;
			List<EnumDefinition>    enums    = [];
			List<RequestDefinition> requests = [];
			List<EventDefinition>   events   = [];
			string interfaceName = child.GetAttribute("name");
			int interfaceVersion = int.Parse(child.GetAttribute("version"));

			foreach (XmlElement interfaceElement in child.OfType<XmlElement>())
			{
				switch (interfaceElement.Name)
				{
					case "description":
						if (summary is not null)
							throw new XmlException("Description has already been specified for this interface.");
						summary = interfaceElement.GetAttribute("summary");
						description = interfaceElement.InnerXml;
						break;
					case "request":
					{
						string requestName = interfaceElement.GetAttribute("name");
						RequestType requestType = interfaceElement.GetAttribute("type") switch
						{
							"" => 0,
							"destructor" => RequestType.Destructor,
							_ => throw new XmlException($"Invalid value '{interfaceElement.GetAttribute("type")}' for 'type' attribute of <request> tag. Acceptable values are 'destructor' or (null).")
						};
						string? requestDescription = null, requestSummary = null;
						List<ArgumentDefinition> requestArgs = [];
						foreach (XmlElement requestElement in interfaceElement.OfType<XmlElement>())
						{
							switch (requestElement.Name)
							{
								case "description":
									if (requestDescription is not null)
										throw new XmlException("Description has already been specified for this request.");
									requestSummary = requestElement.GetAttribute("summary");
									requestDescription = requestElement.InnerXml;
									break;
								case "arg":
									requestArgs.Add(new ( Name      : requestElement.GetAttribute("name"),
									                      Type      : requestElement.GetAttribute("type") switch
														  {
														  	"object"   => ArgumentType.Object,
															"new_id"   => ArgumentType.NewId,
															"uint"     => ArgumentType.UInt,
															"int"      => ArgumentType.Int,
															"array"    => ArgumentType.Array,
															"string"   => ArgumentType.String,
															"fixed"    => ArgumentType.Fixed,
															_ => throw new XmlException($"Invalid value '{requestElement.GetAttribute("type")}' for 'type' attribute of <arg> tag. Valid values are 'object', 'new_id', 'uint', 'int', 'array' or 'string'.")
														  },
									                      Interface : SnakeToPascalCase(requestElement.GetAttribute("interface")),
									                      EnumType  : SnakeToPascalCase(requestElement.GetAttribute("enum")),
									                      Summary   : requestElement.GetAttribute("summary")));
									break;
								default:
									throw new XmlException($"Invalid element <{requestElement.Name}> in <request>. Acceptable elements are <description> and <arg>.");
							}
						}
						requests.Add(new ( Name        : requestName,
						                   Type        : requestType,
						                   Description : requestDescription,
						                   Summary     : requestSummary,
						                   Arguments   : requestArgs.ToArray() ));
					} break;
					case "enum":
					{
						string enumName = interfaceElement.GetAttribute("name");
						string? enumDescription = null, enumSummary = null;
						List<EnumEntry> entries = [];
						foreach (XmlElement enumElement in interfaceElement.OfType<XmlElement>())
						{
							switch (enumElement.Name)
							{
								case "description":
									if (enumDescription is not null)
										throw new XmlException("Description has already been specified for this enum.");
									enumSummary = enumElement.GetAttribute("summary");
									enumDescription = enumElement.InnerXml;
									break;
								case "entry":
									if (!enumElement.IsEmpty)
									{
										if (enumElement.ChildNodes.Count != 1)
											throw new XmlException("A non-self-closing <entry> tag can only contain a single child.");
										var enumChild = enumElement.FirstChild as XmlElement ?? throw new XmlException("child node of <entry> must be an XmlElement.");
										if (enumChild.Name != "description")
											throw new XmlException("Non-self-closing <entry> tags can only contain <description> elements");
										entries.Add(new ( Name     : enumElement.GetAttribute("name"),
														  Value    : ParseInt(enumElement.GetAttribute("value")),
														  Summary  : enumChild.GetAttribute("summary"),
														  Description : enumChild.InnerXml,
														  Since    : int.Parse(enumElement.GetAttributeNode("since")?.Value ?? "0") ));
									}
									else
									{
										entries.Add(new ( Name     : enumElement.GetAttribute("name"),
														  Value    : ParseInt(enumElement.GetAttribute("value")),
														  Summary  : enumElement.GetAttribute("summary"),
														  Since    : int.Parse(enumElement.GetAttributeNode("since")?.Value ?? "0") ));
									}
									break;
								default:
									throw new XmlException($"Invalid element <{enumElement.Name}> in <enum>. Acceptable elements are <description> and <entry>.");
							}
						}
						enums.Add(new ( Name        : enumName!,
						                Description : enumDescription,
						                Summary     : enumSummary,
						                Bitfield    : interfaceElement.GetAttribute("bitfield") == "true",
						                Entries     : entries.ToArray() ));
					} break;
					case "event":
					{
						string eventName = interfaceElement.GetAttribute("name");
						string? eventDescription = null, eventSummary = null;
						List<ArgumentDefinition> eventArgs = [];
						foreach (XmlElement eventElement in interfaceElement.OfType<XmlElement>())
						{
							switch (eventElement.Name)
							{
								case "description":
									if (eventDescription is not null)
										throw new XmlException("Description has already been specified for this event.");
									eventSummary = eventElement.GetAttribute("summary");
									eventDescription = eventElement.InnerXml;
									break;
								case "arg":
									eventArgs.Add(new ( Name      : eventElement.GetAttribute("name"),
									                    Type      : eventElement.GetAttribute("type") switch
														{
															"object"   => ArgumentType.Object,
														    "new_id"   => ArgumentType.NewId,
														    "uint"     => ArgumentType.UInt,
														    "int"      => ArgumentType.Int,
														    "array"    => ArgumentType.Array,
														    "string"   => ArgumentType.String,
															"fixed"    => ArgumentType.Fixed,
														    _ => throw new XmlException($"Invalid value '{eventElement.GetAttribute("type")}' for 'type' attribute of <arg> tag. Valid values are 'object', 'new_id', 'uint', 'int', 'array' or 'string'.")
														},
									                    Interface : eventElement.GetAttribute("interface"),
									                    Summary   : eventElement.GetAttribute("summary") ));
									break;
								default:
									throw new XmlException($"Invalid element <{eventElement.Name}> in <event>. Acceptable elements are <description> and <arg>.");
							}
						}
						events.Add(new ( Name        : eventName,
						                 Description : eventDescription,
						                 Summary     : eventSummary,
						                 Arguments   : eventArgs.ToArray() ));
					} break;
					default:
						throw new XmlException($"Invalid element <{interfaceElement.Name}> in <interface>. Acceptble elements are <description>, <request>, <event> and <enum>.");
				}

			}
			interfaces.Add(new ( Name        : interfaceName,
			                     Description : description,
			                     Summary     : summary,
			                     Version     : interfaceVersion,
			                     Requests    : requests.ToArray(),
			                     Enums       : enums.ToArray(),
			                     Events      : events.ToArray() ));
		} break;
	}
}

Console.WriteLine($"namespace WaySharp.Protocol.{protocolName};");
foreach (var interfaceDefinition in interfaces)
{
	Console.WriteLine($"[WaylandInterface(\"{interfaceDefinition.Name}\", {interfaceDefinition.Version})]");
	Console.WriteLine($"public interface {SnakeToPascalCase(interfaceDefinition.Name)} : IWaylandInterface");
	Console.WriteLine( "{");
	if (interfaceDefinition.Requests.Length != 0)
	{
		int iRequest = -1;
		foreach (RequestDefinition requestDefinition in interfaceDefinition.Requests)
		{
			++iRequest;
			WriteXmldoc("summary", 1, requestDefinition.Summary);
			WriteXmldoc("description", 1, requestDefinition.Description);

			foreach (var arg in requestDefinition.Arguments)
			{
				if (!string.IsNullOrWhiteSpace(arg.Summary))
				{
					if (arg.Type == ArgumentType.NewId)
						Console.WriteLine($"\t/// <returns>{arg.Summary}</returns>");
					else
						Console.WriteLine($"\t/// <param name=\"{SnakeToCamelCase(arg.Name)}\">{arg.Summary}</param>");
				}
			}

			Console.Write($"\t[InterfaceMethod({iRequest}, \"{requestDefinition.Name}\"");
			if (requestDefinition.Type.HasFlag(RequestType.Destructor))
				Console.Write(", InterfaceFlags.Destroy");
			if (requestDefinition.Since != 0)
				Console.Write($", Since={requestDefinition.Since}");
			Console.WriteLine(")]");

			var arguments = requestDefinition.Arguments;
			if (arguments.Length > 0 && arguments[0].Type == ArgumentType.NewId)
			{
				Console.Write('\t' + requestDefinition.Arguments[0].Interface);
				arguments = arguments[1..];
			}
			else
				Console.Write("\tvoid");

			Console.WriteLine($" {SnakeToPascalCase(requestDefinition.Name)}({string.Join(", ", arguments.Select(arg=>GetArgumentType(arg.Type, arg.Interface, arg.EnumType) + " " + SnakeToCamelCase(arg.Name)))});");
			Console.WriteLine();
		}
	}

	if (interfaceDefinition.Events.Length != 0)
	{
		int iEvent = -1;
		foreach (var @event in interfaceDefinition.Events)
		{
			++iEvent;
			WriteXmldoc("summary", 1, @event.Summary);
			WriteXmldoc("remarks", 1, @event.Description);

			foreach (var arg in @event.Arguments)
			{
				if (!string.IsNullOrWhiteSpace(arg.Summary))
				{
					if (arg.Type == ArgumentType.NewId)
						Console.WriteLine($"\t/// <returns>{arg.Summary}</returns>");
					else
						Console.WriteLine($"\t/// <param name=\"{SnakeToCamelCase(arg.Name)}\">{arg.Summary}</param>");
				}
			}

			Console.Write($"\t[InterfaceEvent({iEvent}, \"{@event.Name}\"");
			if (@event.Since != 0)
				Console.Write($", Since={@event.Since}");
			Console.WriteLine(")]");

			Console.Write("\tevent Action");
			if (@event.Arguments.Length > 0)
			{
				Console.Write('<');
				Console.Write(string.Join(", ", @event.Arguments.Select(arg=>GetArgumentType(arg.Type, arg.Interface, arg.EnumType))));
				Console.Write('>');
			}

			Console.WriteLine($" {SnakeToPascalCase(@event.Name)};");
			Console.WriteLine();
		}
	}
	Console.WriteLine('}');
	Console.WriteLine();
}

foreach (var interfaceDefinition in interfaces)
{
	foreach (var @enum in interfaceDefinition.Enums)
	{
		WriteXmldoc("summary", 0, @enum.Summary);
		WriteXmldoc("remarks", 0, @enum.Description);

		if (@enum.Bitfield)
			Console.WriteLine("[Flags]");

		Console.WriteLine($"public enum {SnakeToPascalCase(@enum.Name)}");
		Console.WriteLine('{');
		foreach (var entry in @enum.Entries)
		{
			WriteXmldoc("summary", 1, entry.Summary);
			WriteXmldoc("remarks", 1, entry.Description);
			Console.WriteLine($"\t{SnakeToPascalCase(entry.Name)} = {entry.Value},");
		}
		Console.WriteLine('}');
		Console.WriteLine();
	}
}

return 0;

static string? SnakeToPascalCase(string? snake)
{
	if (string.IsNullOrWhiteSpace(snake))
		return null;

	StringBuilder sb = new StringBuilder(snake.Length);
	bool underscore = true;
	foreach (var ch in snake)
	{
		if (ch == '_')
		{
			underscore = true;
			continue;
		}

		if (underscore)
		{
			sb.Append(char.ToUpper(ch));
			underscore = false;
		}
		else
			sb.Append(ch);
	}

	return sb.ToString();
}

static string? SnakeToCamelCase(string snake)
{
	if (string.IsNullOrWhiteSpace(snake))
		return null;

	StringBuilder sb = new StringBuilder(snake.Length);
	bool underscore = false;
	foreach (var ch in snake)
	{
		if (ch == '_')
		{
			underscore = true;
			continue;
		}

		if (underscore)
		{
			sb.Append(char.ToUpper(ch));
			underscore = false;
		}
		else
			sb.Append(ch);
	}

	string result = sb.ToString();
	if (result == "interface")
		return "@interface";
	return result;
}

static void WriteXmldoc(string tag, int indent, string? text)
{
	if (string.IsNullOrWhiteSpace(text))
		return;

	string[] paragraphs;
	{
		List<string> tempParagraphs = [string.Empty];
		foreach (var line in text.EnumerateLines())
		{
			var trimmedLine = line.Trim(" \t-");
			if (trimmedLine.Length == 0)
				tempParagraphs.Add(string.Empty);
			else
				tempParagraphs[^1] += ' ' + trimmedLine.ToString();
		}
		paragraphs = tempParagraphs.Select(p=>p.Trim()).Where(p=>!string.IsNullOrWhiteSpace(p)).ToArray();
	}

	string prefix = new string('\t', indent);

	if (paragraphs.Length > 1)
	{
		Console.WriteLine($"{prefix}/// <{tag}>");
		foreach (var paragraph in paragraphs)
			Console.WriteLine($"{prefix}///   <para>{paragraph}</para>");
		Console.WriteLine($"{prefix}/// </{tag}>");
	}
	else
	{
		Console.WriteLine($"{prefix}/// <{tag}>{paragraphs[0]}</{tag}>");
	}
}

static int ParseInt(string? text)
{
	if (string.IsNullOrWhiteSpace(text))
		throw new ArgumentNullException(nameof(text));

	if (text.Length > 2 && text[0] == '0' && text[1] == 'x')
		return int.Parse(text[2..], System.Globalization.NumberStyles.AllowHexSpecifier);

	return int.Parse(text);
}

static string GetArgumentType(ArgumentType type, string? @interface, string? @enum) => type switch
{
	ArgumentType.Object => SnakeToPascalCase(@interface) ?? throw new InvalidOperationException("argument is 'object' but specifies no interface type."),
	ArgumentType.NewId  => SnakeToPascalCase(@interface) ?? throw new InvalidOperationException("argument is 'new_id' but specifies no interface type."),
	ArgumentType.Int    => SnakeToPascalCase(@enum) ?? "int",
	ArgumentType.UInt   => SnakeToPascalCase(@enum) ?? "uint",
	ArgumentType.String => "string",
	ArgumentType.Fixed  => "FixedPoint",
	ArgumentType.Array  => "IntPtr",
	_                   => throw new InvalidOperationException($"invalid argument type '{type}'")
};

record ArgumentDefinition(string Name, ArgumentType Type, string? Interface, string? Summary, string? EnumType = null);
record RequestDefinition(string Name, RequestType Type, string? Description, string? Summary, ArgumentDefinition[] Arguments, int Since = 0);
record EnumEntry(string Name, int Value, string? Summary, int Since = 0, string? Description = null);
record EnumDefinition(string Name, string? Description, string? Summary, EnumEntry[] Entries, int Since = 0, bool Bitfield = false);
record EventDefinition(string Name, string? Description, string? Summary, ArgumentDefinition[] Arguments, int Since = 0);
record InterfaceDefinition(string Name, string? Description, string? Summary, int Version, RequestDefinition[] Requests, EnumDefinition[] Enums, EventDefinition[] Events);

enum RequestType
{
	Destructor = 1
}

enum ArgumentType
{
	Object,
	NewId,
	UInt,
	Int,
	Array,
	String,
    Fixed,
}
