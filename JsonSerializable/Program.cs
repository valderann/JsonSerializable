// See https://aka.ms/new-console-template for more information

using JsonSerialize;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;

if (args.Length == 0)
{
    PrintHelp();
    return;
}

var isVerbose = args.Contains("-v");

var path = args[0];
Console.WriteLine(path);
if (!File.Exists(path) && !Directory.Exists(path))
{
    Console.WriteLine("Not a file or directory");
    return;
}

// default .net core directory: C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.3\ref\net6.0
var assemblyNames = GetAssemblyNames(path);
foreach (var assemblyName in assemblyNames)
{
    try
    {
        var assembly = Assembly.LoadFrom(assemblyName);
        var query = assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsPublic &&
                        (t.GetConstructors().Any(v => v.IsPublic && v.GetParameters().Length == 0) ||
                         t.GetConstructors().Count(v => v.IsPublic && v.GetParameters().Length > 0) == 1
                         && !t.Name.EndsWith("Exception")
                         && !t.ContainsGenericParameters
                        ));

        if (args.Contains("-pw"))
        {
            query = query.Where(t => t.GetProperties().Any(v => v.CanWrite));
        }

        if (args.Contains("-s"))
        {
            query = query.Where(t => t.IsSerializable);
        }

        if (!args.Contains("-a"))
        {
            query = query.Where(t => t.Namespace != null && !t.Namespace.StartsWith("System"));
        }

        var types = query.ToArray();
        if (types.Any())
        {
            Console.WriteLine();
            Console.WriteLine($"Analyzing {assemblyName}");
        }

        var results = GetSerializableObjects(types);
        if (isVerbose)
        {
            foreach (var result in results)
            {
                Console.WriteLine($"  {result.TypeName}({result.Constructor})");
                if (!string.IsNullOrWhiteSpace(result.SerializationError))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    With error: {result.SerializationError}");
                    Console.ResetColor();
                }
            }
        }
    }
    catch (Exception exc)
    {
        if (isVerbose)
        {
            Console.WriteLine($"Error parsing assembly: {exc.Message}");
        }
    }
}

IEnumerable<SerializableObject> GetSerializableObjects(Type[] types)
{
    foreach (var type in types)
    {
        if (type.Name.EndsWith("EventHandler")) continue;
        var exception = TestSerialization(type);

        yield return new SerializableObject
        {
            AssemblyName = type?.Assembly?.FullName ?? string.Empty,
            TypeName = type?.Name ?? string.Empty,
            Constructor = GetConstructorParameters(type),
            Namespace = type?.Namespace ?? string.Empty,
            SerializationError = exception,
            Properties = GetProperties(type).ToArray()
        };
    }
}

IEnumerable<SerializationProperty> GetProperties(Type type)
{
    foreach (var property in type.GetProperties().Where(t => t.CanWrite))
    {
        yield return new SerializationProperty()
        {
            CanWrite = property.CanWrite,
            Name = property.Name
        };
    }
}

void PrintHelp()
{
    Console.WriteLine("Returns a list of serializable C# types in .net core");
    Console.WriteLine("Example: JsonSerializable.exe (directory or file path) -v -a");
    Console.WriteLine(" -v verbose, prints verbose messages to the console");
    Console.WriteLine(" -s serializable, only returns types marked as serializable");
    Console.WriteLine(" -a all, also analyze system assemblies");
    Console.WriteLine(" -pw properties writable only, only analyze types that have writable properties");
}

string TestSerialization(Type type)
{
    try
    {
        var settings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        var serialize = Activator.CreateInstance(type, GetParameters(type).ToArray());
        var jsonString = JsonConvert.SerializeObject(serialize, settings);
        JsonConvert.DeserializeObject(jsonString, settings);
    }
    catch (TargetInvocationException exc)
    {
        return exc.InnerException?.Message ?? string.Empty;
    }
    catch (Exception exc)
    {
        return exc.Message;
    }

    return string.Empty;
}

string GetConstructorParameters(Type type)
{
    var outputString = new StringBuilder();
    var firstConstructor = type.GetConstructors().FirstOrDefault();
    if (firstConstructor == null) return string.Empty;


    foreach (var argument in firstConstructor.GetParameters())
    {
        if (argument.Position > 0)
        {
            outputString.Append(", ");
        }
        outputString.Append(argument.ParameterType);
        outputString.Append(" ");
        outputString.Append(argument.Name);
    }

    return outputString.ToString();
}

IEnumerable<object?> GetParameters(Type type)
{
    if (type.GetConstructors().Any(t => t.GetParameters().Length == 0)) yield break;

    var firstConstructor = type.GetConstructors().FirstOrDefault();
    if (firstConstructor == null) yield break;
    foreach (var argument in firstConstructor.GetParameters())
    {
        if (argument.ParameterType == typeof(string))
        {
            yield return "parameter";
        }
        else
        {
            yield return null;
        }
    }
}

string[] GetAssemblyNames(string filePath)
{
    if (File.Exists(filePath))
    {
        return new[] { path };
    }

    if (Directory.Exists(filePath))
    {
        return Directory.GetFiles(filePath, "*.dll");
    }

    return Array.Empty<string>();
}