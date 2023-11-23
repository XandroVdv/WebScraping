using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;



class Program
{
    //the amount of items to be shown
    private const int MaxItemsToProcess = 5;
    static void Main()
    {
        // interaction with user to enable the choice between the different options.
        while (true)
        {
            Console.WriteLine("Hello, Welcome to my web scraper. Please choose an option:");
            Console.WriteLine("1 -> YouTube, 2 -> ICTJOB, 3 -> Zalando, 0 -> Exit");

            if (!int.TryParse(Console.ReadLine(), out int i))
            {
                Console.WriteLine("Please enter a valid option!");
                continue;
            }
            if (i == 0)
            {
                Console.WriteLine("Exiting the program. Goodbye!");
                break;
            }
            switch (i)
            {
                case 1:
                    Console.WriteLine("What search term would you like to use for YouTube?");
                    List<VideoData> videoDataList = scrapeYoutube(Console.ReadLine());
                    PrintVideoData(videoDataList);
                    break;
                case 2:
                    Console.WriteLine("What search term would you like to use for ICTJOB?");
                    List<JobData> jobDataList = scrapeICTJOB(Console.ReadLine());
                    PrintJobData(jobDataList);

                    break;
                case 3:
                    Console.WriteLine("What search term would you like to use for Zalando?");
                    List<ProductData> productDataList = scrapeZalando(Console.ReadLine());
                    PrintProductData(productDataList);
                    break;
                default:
                    Console.WriteLine("Please enter a valid option!");
                    break;
            }
        }
    }
    /// <summary>
    /// Method used to scrape Youtube for information on the first fiew videos from a searchterm
    /// </summary>
    /// <param name="searchTerm"></param>
    /// <returns></returns>
    static List<VideoData> scrapeYoutube(string searchTerm)
    {
        string url = $"https://www.youtube.com/results?search_query={searchTerm}";

        using (IWebDriver driver = new ChromeDriver())
        {
            driver.Navigate().GoToUrl(url);

            Thread.Sleep(5000);

            List<VideoData> videoDataList = new List<VideoData>();

            for (int i = 1; i <= MaxItemsToProcess; i++)
            {
                IWebElement result = driver.FindElement(By.XPath($"//div[@id='contents']//ytd-video-renderer[{i}]"));

                VideoData videoData = new VideoData
                {
                    Title = result.FindElement(By.Id("video-title")).Text,
                    Link = result.FindElement(By.Id("video-title")).GetAttribute("href"),
                    Uploader = result.FindElement(By.CssSelector("ytd-video-renderer #channel-info #text-container yt-formatted-string a")).Text,
                    Views = result.FindElement(By.CssSelector("#metadata-line .inline-metadata-item")).Text,
                };

                videoDataList.Add(videoData);
            }
            saveData(videoDataList);
            driver.Quit();
            return videoDataList;
        }
    }
    /// <summary>
    /// Method used to scrape ictjob.be for a job.
    /// </summary>
    /// <param name="searchTerm"></param>
    /// <returns></returns>
    static List<JobData> scrapeICTJOB(string searchTerm)
    {
        string url = $"https://www.ictjob.be/nl/it-vacatures-zoeken?q={searchTerm}";

        using (IWebDriver driver = new ChromeDriver())
        {
            driver.Navigate().GoToUrl(url);

            // Wait for the page to load (you may need to adjust the wait time)
            Thread.Sleep(5000);

            // Extract information from the job listings
            List<JobData> jobDataList = new List<JobData>();

            // Select all the job items
            IReadOnlyCollection<IWebElement> jobItems = driver.FindElements(By.CssSelector(".search-item"));

            // Filter out non-job elements (ads)
            List<IWebElement> filteredJobItems = jobItems
                .Where(jobItem =>
                {
                    try
                    {
                        // Check if the job item has a title element
                        jobItem.FindElement(By.CssSelector("[itemprop='title']"));
                        return true;
                    }
                    catch (NoSuchElementException)
                    {
                        Console.WriteLine("Warning: Something that did not look like a job was removed!");
                        return false;
                    }
                })
                .ToList();

            // Process only the top 5 job items
            for (int i = 1; i < Math.Min(MaxItemsToProcess, filteredJobItems.Count); i++)
            {
                var jobItem = filteredJobItems.ElementAt(i);

                JobData jobData = new JobData
                {
                    Title = jobItem.FindElement(By.CssSelector(".job-title")).Text,
                    Company = jobItem.FindElement(By.CssSelector(".job-company")).Text,
                    Location = jobItem.FindElement(By.CssSelector(".job-location")).Text,
                    Keywords = GetOrDefault(() => jobItem.FindElement(By.CssSelector(".job-keywords")).Text, "none"),
                    Link = jobItem.FindElement(By.CssSelector(".job-title")).GetAttribute("href"),
                };

                jobDataList.Add(jobData);
            }
            saveData(jobDataList);
            driver.Quit();
            return jobDataList;
        }
    }
    /// <summary>
    /// Method to scrape zalando for clothing articles
    /// </summary>
    /// <param name="searchTerm"></param>
    /// <returns></returns>
    static List<ProductData> scrapeZalando(string searchTerm)
    {
        string url = $"https://www.zalando.be/catalogus/?q={searchTerm}";

        using (IWebDriver driver = new ChromeDriver())
        {
            driver.Navigate().GoToUrl(url);

            // Wait for the page to load (you may need to adjust the wait time)
            Thread.Sleep(5000);

            // Extract information from the first 5 products
            List<ProductData> productDataList = new List<ProductData>();

            // Select all the product items
            IReadOnlyCollection<IWebElement> productItems = driver.FindElements(By.CssSelector("article header"));

            // Process only the top 5 product items
            for (int i = 1; i < Math.Min(MaxItemsToProcess, productItems.Count); i++)
            {
                var productItem = productItems.ElementAt(i);
                string price = GetOrDefault(() => productItem.FindElement(By.CssSelector("header section span:last-child")).Text, "0,00");
                string[] splitPrice = price.Split(' ');

                IWebElement linkElement = productItem.FindElement(By.XPath("./ancestor::a"));

                // Get the href attribute value
                string hrefValue = linkElement.GetAttribute("href");
                ProductData productData = new ProductData
                {
                    Name = GetOrDefault(() => productItem.FindElement(By.CssSelector("header .FtrEr_")).Text, "No production name found!"),
                    Price = splitPrice[1],
                    Link = hrefValue,
                };

                productDataList.Add(productData);
            }
            saveData(productDataList);
            driver.Quit();
            return productDataList;
        }
    }


    /// <summary>
    /// Helper method to get a value or a default value if an exception occurs
    /// </summary>
    /// <param name="getValue"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    private static string GetOrDefault(Func<string> getValue, string defaultValue)
    {
        try
        {
            return getValue.Invoke();
        }
        catch (NoSuchElementException)
        {
            return defaultValue;
        }
    }
    /// <summary>
    /// Method saves a list of data into a CSV and JSON file inside of the project directory.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dataList"></param>
    private static void saveData<T>(List<T> dataList)
    {
        // Writing data to CSV file
        try
        {
            using (var writer = new StreamWriter("saved_data.csv", append: true))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteRecords(dataList);
            }
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not save to CSV!");
            Console.ResetColor();
        }

        try
        {        
            // Writing data to JSON file
            string jsonString = JsonSerializer.Serialize(dataList, new JsonSerializerOptions
            {
                WriteIndented = true, // Makes the JSON output more readable
            });

            File.AppendAllText("saved_data.json", jsonString);

        }
        catch 
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not save to JSON!");
            Console.ResetColor();
        }
    }
    /// <summary>
    /// Method prints the list of videos scraped from youtube
    /// </summary>
    /// <param name="videoDataList"></param>
    static void PrintVideoData(List<VideoData> videoDataList)
    {
        foreach (var videoData in videoDataList)
        {
            Console.WriteLine($"Title: {videoData.Title}\nLink: {videoData.Link}\nUploader: {videoData.Uploader}\nViews: {videoData.Views}\n");
        }
    }
    /// <summary>
    /// Method prints the list of jobs scraped from ictjob
    /// </summary>
    /// <param name="jobDataList"></param>
    static void PrintJobData(List<JobData> jobDataList)
    {
        foreach (var jobData in jobDataList)
        {
            Console.WriteLine($"Title: {jobData.Title}\nCompany: {jobData.Company}\nLocation: {jobData.Location}\nKeywords: {jobData.Keywords}\nLink: {jobData.Link}\n");
        }
    }
    /// <summary>
    /// Method prints the list of articles scraped from zalando
    /// Prints total value and average of scraped articles
    /// </summary>
    /// <param name="productDataList"></param>
    static void PrintProductData(List<ProductData> productDataList)
    {
        double price = 0;
        List<double> priceList = new List<double>();
        foreach (var productData in productDataList)
        {
            priceList.Add(Convert.ToDouble(productData.Price));
            price += Convert.ToDouble(productData.Price);
            Console.WriteLine($"Name: {productData.Name}\nPrice: {productData.Price}\nLink: {productData.Link}\n");
        }
        Console.WriteLine($"Total price of these articles is: {Math.Round(price, 2)}");
        Console.WriteLine($"Average price of these articles is: {Queryable.Average(priceList.AsQueryable())}");

    }
}
// classes
class VideoData
{
    public string Title { get; set; }
    public string Link { get; set; }
    public string Uploader { get; set; }
    public string Views { get; set; }
}
class JobData
{
    public string Title { get; set; }
    public string Company { get; set; }
    public string Location { get; set; }
    public string Keywords { get; set; }
    public string Link { get; set; }
}

class ProductData
{
    public string Name { get; set; }
    public string Price { get; set; }
    public string Link { get; set; }
}