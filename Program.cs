using HtmlAgilityPack;

namespace khinsider_scraper_c_;

internal abstract class Program
{
    private static readonly HtmlWeb Scraper = new();
    private static readonly HttpClient Client = new();

    private static async Task Main(string[] args)
    {
        List<string> downloadPageContent = [];

        if (args.Length > 0)
        {
            var initialPageContent = await InitialPageScrape(args[0]);
            downloadPageContent = await DownloadPageScrape(initialPageContent);
        }
        else
        {
            Console.WriteLine("[USAGE]: program.exe <URL>");
            Console.WriteLine(
                "[ERROR]: No URL provided, please provide the link after the executable."
            );
            Environment.Exit(1);
        }

        foreach (var link in downloadPageContent)
        {
            var filename = Uri.UnescapeDataString(link.Split('/')[6]);

            while (true)
            {
                Console.Write($"Download |{filename}| y/n/q?   ");
                var input = Console.ReadLine()?.Trim().ToLower() ?? "q";

                switch (input)
                {
                    case "y":
                        await DownloadFileAsync(link, filename);
                        break;

                    case "n":
                        break;

                    case "q":
                        return;

                    default:
                        Console.WriteLine("Illegal keypress, try again.");
                        continue;
                }
                break;
            }
        }
    }

    private static async Task<List<string>> InitialPageScrape(string initialPage)
    {
        List<string> aTags = [];
        var page = await Scraper.LoadFromWebAsync(initialPage);
        var tdTags = page.DocumentNode.SelectNodes(
            "//td[contains(concat(' ', normalize-space(@class), ' '), ' playlistDownloadSong ')]"
        );
        aTags.AddRange(tdTags.SelectMany(td => td.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>())
            .Select(a => a.GetAttributeValue("href", ""))
            .Select(tdTag => $"https://downloads.khinsider.com{tdTag}"));
        return aTags;
    }

    private static async Task<List<string>> DownloadPageScrape(List<string> hrefs)
    {
        List<string> downloadLinks = [];
        IEnumerable<Task<IEnumerable<string>>> tasks = hrefs.Select(async href =>
        {
            var page = await Scraper.LoadFromWebAsync(href);
            var aTags =
                page.DocumentNode.SelectNodes("//a") ?? Enumerable.Empty<HtmlNode>();

            return aTags
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(link => link.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));
        });
        var results = await Task.WhenAll(tasks);

        foreach (var flacLinks in results)
        {
            downloadLinks.AddRange(flacLinks);
        }

        return downloadLinks;
    }

    static async Task DownloadFileAsync(
        string fileUrl,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        await using var fileSteam = await Client.GetStreamAsync(fileUrl, cancellationToken);
        await using FileStream ioManager = new(
            fileName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            8196,
            true
        );
        await fileSteam.CopyToAsync(ioManager, cancellationToken);
        await ioManager.FlushAsync(cancellationToken);
    }
}
