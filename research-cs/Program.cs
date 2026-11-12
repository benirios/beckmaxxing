using ResearchCandidates;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project research-cs -- a|b|c|exp015|exp016 [mode arguments]. See research-cs/README.md");
    return 2;
}
try
{
    switch (args[0].ToLowerInvariant())
    {
        case "a": CandidateA.Run(args); break;
        case "b": CandidateB.Run(args); break;
        case "c": CandidateC.Run(args); break;
        case "exp015": Exp015AsiaRebound.Run(args); break;
        case "exp016": Exp016OvernightDrift.Run(args); break;
        default: throw new ArgumentException("Mode must be a, b, or c");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
