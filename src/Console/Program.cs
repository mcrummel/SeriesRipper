using ConsoleApp;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

const string COMMAND = "HandBrakeCLI.exe";
string workingDirectory = string.Join('\\',
    System.Reflection.Assembly.GetExecutingAssembly().Location.Split("\\")[..^1]
);

InputParameters inputParams = new(args);

string title = inputParams.SourceFolder.Split("\\")[^1];

int episodeCounter = inputParams.StartEpisode;
foreach (string seasonDir in Directory.GetDirectories(inputParams.SourceFolder))
{
    int season = GetSeason(seasonDir);
    if (season < 1) continue;

    Queue<string> commands = QueueCommmands(workingDirectory, inputParams, episodeCounter, title, season, seasonDir);
    while (commands.Any())
    {
        string cmd = commands.Dequeue();
        TranscodeEpisode(cmd);
    }

    episodeCounter = 1;
}

string GetScanArguments(string directory) => $@"-t 0 --scan --input ""{directory}""";

string GetTranscodeArguments(string source, string dest, string title, int season, int track, int episode, int? chapter = null)
{
    List<string> lines = new([
        @"--preset-import-file BluRay.json --preset ""Blu Ray""",
        $@"-i ""{source}""",
        $@"-o ""{dest}\{title} - s{season:00}e{episode:00}.mkv""",
        $@"--title {track}"
    ]);

    if (chapter.HasValue)
        lines.Add($"-c {chapter}");

    return string.Join(" ", lines);
}
    

void TranscodeEpisode(string arguments)
{
    ProcessStartInfo psi = new()
    {
        FileName = COMMAND,
        Arguments = arguments,
        RedirectStandardError = true,
        RedirectStandardOutput = false,
        UseShellExecute = false,
        CreateNoWindow = false,
        WorkingDirectory = $"{workingDirectory}"
    };

    Console.WriteLine($"{COMMAND} {psi.Arguments}");
    Process proc = new()
    {
        StartInfo = psi,
    };

    try
    {
        bool started = proc.Start();
        string output = proc.StandardError.ReadLine();
        while (output != null)
        {
            Console.WriteLine(output);
            output = proc.StandardError.ReadLine();
        }

        proc.WaitForExit();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}

Dictionary<int, int> ScanImage(string directory, InputParameters p)
{
    ProcessStartInfo psi = new()
    {
        FileName = COMMAND,
        Arguments = GetScanArguments(directory),
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = false,
        WorkingDirectory = $"{workingDirectory}"
    };

    Console.WriteLine($"{workingDirectory}\\{COMMAND} {psi.Arguments}");

    Process proc = new()
    {
        StartInfo = psi,
    };

    Dictionary<int, int> titles = new();
    try
    {
        bool started = proc.Start();

        string? strTitleCount = ReadUntilMatch(proc.StandardError, ScanFoundTitlesLine());
        if (strTitleCount is null)
            throw new Exception("Unable to find any titles!");

        int titleCount = Convert.ToInt32(strTitleCount);

        while (titleCount > 0)
        {
            string titleStr = ReadUntilMatch(proc.StandardError, FindTitle()) 
                ?? throw new Exception("Unable to find title!");
            int title = Convert.ToInt32(titleStr);

            titleCount--;

            string duration = ReadUntilMatch(proc.StandardError, FindDuration())
                ?? throw new Exception("Unable to find duration!");

            var d = duration.Split(':').Select(_ => int.Parse(_)).ToArray();
            TimeSpan dur = new(d[0], d[1], d[2]);

            if (dur.CompareTo(p.DurationThreshold) >= 0)
            {
                titles[title] = 0;

                ReadUntilMatch(proc.StandardError, FindChapters());
                string? chapterLine = proc.StandardError.ReadLine();
                while (chapterLine != null)
                {
                    var match = FindChapter().Match(chapterLine);
                    if (!match.Success)
                        break;

                    titles[title]++;
                    chapterLine = proc.StandardError.ReadLine();
                }
            }
        }

        proc.WaitForExit();

        Console.WriteLine($"Found {titles.Count} episodes:");
        foreach ((int title, int chapterCount) in titles)
        {
            Console.WriteLine($"Found title {title} with {chapterCount} chapters");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }

    return titles;
}

string? ReadUntilMatch(StreamReader reader, Regex pattern)
{
    string? line = reader.ReadLine();
    while (line != null)
    {
        Console.WriteLine(line);
        var match = pattern.Match(line);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        line = reader.ReadLine();
    }

    return null;
}

Queue<string> QueueCommmands(string workingDirectory, InputParameters inputParams, int episode, string title, int season, string seasonDir)
{
    Queue<string> commands = new();
    foreach (string imageDir in Directory.GetDirectories(seasonDir))
    {
        var tracks = ScanImage(imageDir, inputParams);
        foreach ((int track, int chapterCount) in tracks)
        {
            if (!inputParams.ChaptersAsEpisodes)
            {
                commands.Enqueue(GetTranscodeArguments(imageDir, inputParams.DestinationFolder, title, season, track, episode));
                episode++;
                continue;
            }

            for (int i = 1; i <= chapterCount; i++)
            {
                commands.Enqueue(GetTranscodeArguments(imageDir, inputParams.DestinationFolder, title, season, track, episode, chapter: i));
                episode++;
            }
        }
    }

    return commands;
}

static int GetSeason(string seasonDir)
{
    string[] imageDirs = Directory.GetDirectories(seasonDir);
    var seasonMatch = FindSeasonNumber().Match(seasonDir);
    if (!seasonMatch.Success)
    {
        Console.WriteLine($"Unable to determin season number from directory: {seasonDir}");
        return -1;
    }
    int season = Convert.ToInt32(seasonMatch.Groups[1].Value);
    return season;
}

partial class Program
{
    [GeneratedRegex(@"scan thread found (\d+) valid title")]
    private static partial Regex ScanFoundTitlesLine();

    [GeneratedRegex(@"[+] duration: (\d\d:\d\d:\d\d)")]
    private static partial Regex FindDuration();

    [GeneratedRegex(@"[+] title (\d+)")]
    private static partial Regex FindTitle();

    [GeneratedRegex(@"[+] chapters:")]
    private static partial Regex FindChapters();
    
    [GeneratedRegex(@"[+] (\d+): duration (\d\d:\d\d:\d\d)")]
    private static partial Regex FindChapter();

    [GeneratedRegex(@"[Ss]eason\s+(\d+)")]
    private static partial Regex FindSeasonNumber();
}