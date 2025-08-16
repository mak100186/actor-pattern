using System.Text;

internal class Program
{
    static void Main(string[] args)
    {
        FlattenEntireCodebase();
    }

    private static void FlattenEntireCodebase()
    {
        // Root of your codebase
        var rootPath = @".\..\..\..\..\..\src";

        // Where the flattened output will be written
        var outputFile = @".\..\..\..\..\..\tools\output.txt";

        // File extensions to include
        string[] extensions = { ".cs" };

        var sb = new StringBuilder();

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                     .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            sb.AppendLine($"// ===== File: {file.Replace(rootPath, "").TrimStart(Path.DirectorySeparatorChar)} =====");
            sb.AppendLine(File.ReadAllText(file));
            sb.AppendLine();
        }

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);

        Console.WriteLine($"Flattened codebase written to: {outputFile}");
    }
}