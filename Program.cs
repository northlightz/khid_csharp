using HtmlAgilityPack;

namespace khinsider_scraper_c_;

class Program
{
    static readonly HtmlWeb Scraper = new();
    static readonly HttpClient Client = new();

    static async Task Main(string[] args)
    {
        List<string> InitalPageContent;
        List<string> DownloadPageContent = [];

        if (args.Length > 0)
        {
            InitalPageContent = await InitialPageScrape(args[0]);
            DownloadPageContent = await DownloadPageScrape(InitalPageContent);
        }
        else
        {
            Console.WriteLine("[USAGE]: program.exe <URL>");
            Console.WriteLine(
                "[ERROR]: No URL provided, please provide the link after the executable."
            );
            Environment.Exit(1);
        }

        foreach (string link in DownloadPageContent)
        {
            string filename = Uri.UnescapeDataString(link.Split('/')[6]);

            while (true)
            {
                Console.Write($"Download |{filename}| y/n/q?   ");
                string input = Console.ReadLine()?.Trim().ToLower() ?? "q";

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

    static async Task<List<string>> InitialPageScrape(string initial_page)
    {
        List<string> a_tags = [];
        HtmlDocument page = await Scraper.LoadFromWebAsync(initial_page);
        HtmlNodeCollection td_tags = page.DocumentNode.SelectNodes(
            "//td[contains(concat(' ', normalize-space(@class), ' '), ' playlistDownloadSong ')]"
        );
        foreach (
            string td_tag in td_tags
                .SelectMany(td => td.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>())
                .Select(a => a.GetAttributeValue("href", ""))
        )
        {
            a_tags.Add($"https://downloads.khinsider.com{td_tag}");
        }
        return a_tags;
    }

    static async Task<List<string>> DownloadPageScrape(List<string> hrefs)
    {
        List<string> download_links = [];
        IEnumerable<Task<IEnumerable<string>>> tasks = hrefs.Select(async href =>
        {
            HtmlDocument page = await Scraper.LoadFromWebAsync(href);
            IEnumerable<HtmlNode> a_tags =
                page.DocumentNode.SelectNodes("//a") ?? Enumerable.Empty<HtmlNode>();

            return a_tags
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(link => link.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));
        });
        IEnumerable<string>[] results = await Task.WhenAll(tasks);

        foreach (IEnumerable<string> flacLinks in results)
        {
            download_links.AddRange(flacLinks);
        }

        return download_links;
    }

    static async Task DownloadFileAsync(
        string fileUrl,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        await using Stream file_steam = await Client.GetStreamAsync(fileUrl, cancellationToken);
        await using FileStream io_manager = new(
            fileName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            8196,
            true
        );
        await file_steam.CopyToAsync(io_manager, cancellationToken);
        await io_manager.FlushAsync(cancellationToken);
    }
}
