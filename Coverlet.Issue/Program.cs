using System.Diagnostics;
using Coverlet.Issue;

Console.Write("Old approach: ");
var measure = Stopwatch.StartNew();
var oldApproach = new List<string>();
foreach (var module in Data.MODULES)
{
    if (InstrumentationHelper.IsModuleExcluded(module, Data.EXCLUDE) || !InstrumentationHelper.IsModuleIncluded(module, Data.INCLUDE))
    {
        continue;
    }

    oldApproach.Add(module);
}

Console.WriteLine(measure.Elapsed);

Console.Write("New approach: ");
measure.Restart();
var newApproach = InstrumentationHelper.SelectModules(Data.MODULES, Data.INCLUDE, Data.EXCLUDE).ToList();
Console.WriteLine(measure.Elapsed);
Console.WriteLine();
Console.WriteLine($"Is result similar: {oldApproach.OrderBy(x => x).SequenceEqual(newApproach.OrderBy(x => x))}");
