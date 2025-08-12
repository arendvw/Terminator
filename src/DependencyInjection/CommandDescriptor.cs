using System.Collections.Generic;

namespace Terminator.DependencyInjection;

public class CommandDescriptor
{
    public List<string> CommandPath { get; set; } = new();
    public required string Description { get; set; }

    public string Command => string.Join(" ", CommandPath);
    
    public override string ToString()
    {
        return $"{Command} - {Description}";
    }
}