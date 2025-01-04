namespace ConsoleApp;

internal sealed class InputParameters
{
    public string SourceFolder { get; set; }
    public string DestinationFolder { get; set; }
    public TimeSpan DurationThreshold { get; set; } = TimeSpan.FromMinutes(40);
    public int StartEpisode { get; set; } = 1;

    public bool ChaptersAsEpisodes { get; set; } = false;

    public InputParameters(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i]) {
                case "--source":
                    SourceFolder = args[++i]; break;
                case "--dest":
                    DestinationFolder = args[++i]; break;
                case "--duration-threshold":
                    var d = args[++i].Split(':').Select(_ => int.Parse(_)).ToArray();
                    DurationThreshold = new(d[0], d[1], d[2]);
                    break;
                case "--start-episode":
                    StartEpisode = Convert.ToInt32(args[++i]); break;
                case "--chapter-as-episode":
                    ChaptersAsEpisodes = true; break;
            }
        }
    }
}
