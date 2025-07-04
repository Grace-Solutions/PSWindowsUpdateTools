using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for fetching Windows release history information from Microsoft sources
    /// </summary>
    public class WindowsReleaseHistoryService
    {
        private readonly HttpClient _httpClient;
        private readonly PSCmdlet? _cmdlet;
        private readonly bool _continueOnError;
        private readonly List<string> _warnings = new List<string>();
        private readonly List<(string message, Exception? exception)> _errors = new List<(string, Exception?)>();

        /// <summary>
        /// Gets the Microsoft Learn URLs for Windows release information using current culture
        /// </summary>
        private static string[] GetReleaseHistoryUrls()
        {
            // Get current culture, fallback to en-us if not available
            var cultureInfo = CultureInfo.CurrentCulture;
            var cultureName = cultureInfo.Name.ToLowerInvariant();

            // Fallback to en-us if culture is not supported or is invariant
            if (string.IsNullOrEmpty(cultureName) || cultureName == "iv" || cultureName.Length < 2)
            {
                cultureName = "en-us";
            }

            return new[]
            {
                $"https://learn.microsoft.com/{cultureName}/windows/release-health/release-information",
                $"https://learn.microsoft.com/{cultureName}/windows/release-health/windows11-release-information",
                $"https://learn.microsoft.com/{cultureName}/windows-server/get-started/windows-server-release-info"
            };
        }

        /// <summary>
        /// Initializes a new instance of the WindowsReleaseHistoryService
        /// </summary>
        public WindowsReleaseHistoryService(HttpClient httpClient, PSCmdlet? cmdlet = null, bool continueOnError = false)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cmdlet = cmdlet;
            _continueOnError = continueOnError;
        }

        /// <summary>
        /// Gets Windows release history information from Microsoft sources
        /// </summary>
        public List<WindowsReleaseInfo> GetWindowsReleaseHistory()
        {
            var releaseInfoList = new List<WindowsReleaseInfo>();

            try
            {
                LogVerbose("Constructing Microsoft Learn URLs using current culture...");

                // Get URLs based on current culture
                var urls = GetReleaseHistoryUrls();
                var cultureInfo = CultureInfo.CurrentCulture;
                var cultureName = string.IsNullOrEmpty(cultureInfo.Name) ? "en-us" : cultureInfo.Name.ToLowerInvariant();

                LogVerbose($"Using culture: {cultureName}");
                LogVerbose($"Processing {urls.Length} Microsoft Learn release history URLs");

                // Log all URLs that will be processed
                for (int j = 0; j < urls.Length; j++)
                {
                    LogVerbose($"  URL {j + 1}: {urls[j]}");
                }

                // Process each URL
                for (int i = 0; i < urls.Length; i++)
                {
                    try
                    {
                        var url = urls[i].Trim();
                        if (string.IsNullOrEmpty(url) || url.StartsWith("#"))
                        {
                            LogVerbose($"[{i + 1} of {urls.Length}] Skipping empty or commented URL");
                            continue;
                        }

                        LogVerbose($"[{i + 1} of {urls.Length}] Processing URL: {url}");

                        var releases = ProcessReleaseHistoryUrl(url);
                        releaseInfoList.AddRange(releases);

                        LogVerbose($"[{i + 1} of {urls.Length}] Retrieved {releases.Count} release records from {url}");
                        LogVerbose($"[{i + 1} of {urls.Length}] Total records so far: {releaseInfoList.Count}");
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"[{i + 1} of {urls.Length}] Failed to process URL {urls[i]}: {ex.Message}");

                        if (!_continueOnError)
                            throw;
                    }

                    LogVerbose($"[{i + 1} of {urls.Length}] Completed processing URL {i + 1}");
                }

                LogVerbose($"Successfully retrieved {releaseInfoList.Count} total release records");
            }
            catch (Exception ex)
            {
                LogError($"Failed to fetch Windows release history: {ex.Message}", ex);

                if (!_continueOnError)
                    throw;
            }

            return releaseInfoList;
        }

        /// <summary>
        /// Processes a single release history URL
        /// </summary>
        private List<WindowsReleaseInfo> ProcessReleaseHistoryUrl(string url)
        {
            var releaseInfoList = new List<WindowsReleaseInfo>();

            try
            {
                // Load the HTML document
                var web = new HtmlWeb();
                var doc = web.Load(url);

                // Determine regex patterns based on URL
                var isServerUrl = url.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
                var osNameRegex = isServerUrl 
                    ? new Regex(@"(Windows\s+Server\s+\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    : new Regex(@"(Windows\s+\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

                var osInfoRegex = isServerUrl
                    ? new Regex(@"(?<OSName>.+\s+.+\s+\d{4,})(?:\s+)?(?:.+)(?<BuildNumber>\d{5,})(?:.+)?", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    : new Regex(@"(?:.+)?(?:Version)(?:\s+)?(?<ReleaseID>[a-z0-9]{4,})(?:\s+)?(?:.+)(?<BuildNumber>\d{5,})(?:.+)?", RegexOptions.IgnoreCase | RegexOptions.Multiline);

                // Find history tables
                var historyTables = doc.DocumentNode.SelectNodes("//table[contains(@id,'historyTable_')]");
                
                if (historyTables == null)
                {
                    LogWarning($"No history tables found in URL: {url}");
                    return releaseInfoList;
                }

                foreach (var table in historyTables)
                {
                    try
                    {
                        var releaseInfo = ProcessHistoryTable(table, osNameRegex, osInfoRegex, isServerUrl, doc);
                        if (releaseInfo != null)
                        {
                            releaseInfoList.Add(releaseInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Failed to process history table: {ex.Message}");
                        
                        if (!_continueOnError)
                            throw;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to process URL {url}: {ex.Message}", ex);
                
                if (!_continueOnError)
                    throw;
            }

            return releaseInfoList;
        }

        /// <summary>
        /// Processes a single history table to extract release information
        /// </summary>
        private WindowsReleaseInfo? ProcessHistoryTable(HtmlNode table, Regex osNameRegex, Regex osInfoRegex, bool isServerUrl, HtmlDocument doc)
        {
            try
            {
                // Get operating system info from preceding strong element
                var osInfoNode = table.SelectSingleNode("preceding::strong[position()=1]");
                if (osInfoNode == null)
                    return null;

                var osInfoText = osInfoNode.InnerText;
                var osInfoMatch = osInfoRegex.Match(osInfoText);
                
                if (!osInfoMatch.Success)
                    return null;

                // Extract build number
                if (!int.TryParse(osInfoMatch.Groups["BuildNumber"].Value, out var buildNumber))
                    return null;

                // Extract operating system name
                var operatingSystem = isServerUrl
                    ? osNameRegex.Match(osInfoText).Value
                    : osNameRegex.Match(doc.DocumentNode.OuterHtml).Value;

                // Create release info object
                var releaseInfo = new WindowsReleaseInfo
                {
                    OperatingSystem = operatingSystem,
                    Type = isServerUrl ? "Server" : "Client",
                    ReleaseID = isServerUrl
                        ? Regex.Match(operatingSystem, @"\d{4}").Value
                        : osInfoMatch.Groups["ReleaseID"].Value,
                    InitialReleaseVersion = new Version(10, 0, buildNumber, 0)
                };

                // Process table rows to get releases
                var releases = ProcessTableRows(table);
                releaseInfo.Releases = releases.ToArray();

                // Determine initial release version and LTSC status
                if (releases.Any())
                {
                    var initialRelease = releases.OrderBy(r => r.AvailabilityDate).First();
                    releaseInfo.InitialReleaseVersion = initialRelease.Version;
                    
                    releaseInfo.HasLongTermServicingBuild = releases.Any(r =>
                        r.ServicingOptions.Any(s =>
                            s.IndexOf("LTSC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.IndexOf("LTSB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.IndexOf("Long Term", StringComparison.OrdinalIgnoreCase) >= 0));
                }

                // Mark the latest release
                MarkLatestRelease(releaseInfo);

                return releaseInfo;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to process history table: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Processes table rows to extract individual releases
        /// </summary>
        private List<WindowsRelease> ProcessTableRows(HtmlNode table)
        {
            var releases = new List<WindowsRelease>();

            try
            {
                var headerRow = table.SelectSingleNode(".//tr[position()=1]");
                var dataRows = table.SelectNodes(".//tr[position()>1]");

                if (headerRow == null || dataRows == null)
                    return releases;

                var headers = headerRow.SelectNodes(".//th")?.Select(th => th.InnerText.Trim()).ToArray();
                if (headers == null)
                    return releases;

                foreach (var row in dataRows)
                {
                    try
                    {
                        var cells = row.SelectNodes(".//td");
                        if (cells == null || cells.Count != headers.Length)
                            continue;

                        var release = ProcessTableRow(headers, cells);
                        if (release != null)
                        {
                            releases.Add(release);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Failed to process table row: {ex.Message}");
                        
                        if (!_continueOnError)
                            throw;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to process table rows: {ex.Message}");
                
                if (!_continueOnError)
                    throw;
            }

            return releases;
        }

        /// <summary>
        /// Processes a single table row to create a WindowsRelease object
        /// </summary>
        private WindowsRelease? ProcessTableRow(string[] headers, HtmlNodeCollection cells)
        {
            try
            {
                var release = new WindowsRelease();

                for (int i = 0; i < headers.Length && i < cells.Count; i++)
                {
                    var header = headers[i].Replace(" ", "").ToLowerInvariant();
                    var cellValue = cells[i].InnerText.Trim();

                    switch (header)
                    {
                        case "availabilitydate":
                            if (FormatUtilityService.TryParseDate(cellValue, out var date))
                                release.AvailabilityDate = date.Date;
                            break;

                        case "build":
                        case "osbuild":
                            if (FormatUtilityService.TryParseVersion(cellValue, out var version))
                                release.Version = version;
                            break;

                        case "kbarticle":
                            var normalizedKB = FormatUtilityService.NormalizeKBArticle(cellValue);
                            if (!string.IsNullOrEmpty(normalizedKB))
                                release.KBArticle = normalizedKB;

                            var linkNode = cells[i].SelectSingleNode(".//a");
                            if (linkNode != null)
                            {
                                var href = linkNode.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href) && Uri.TryCreate(href, UriKind.Absolute, out var uri))
                                    release.KBArticleURL = uri;
                            }
                            break;

                        case var servicingHeader when servicingHeader.IndexOf("servicing", StringComparison.OrdinalIgnoreCase) >= 0:
                            var servicingOptions = cellValue
                                .Replace("[^a-z0-9\\s]", "")
                                .Replace("\\s{2,}", ",")
                                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToArray();
                            release.ServicingOptions = servicingOptions;
                            break;
                    }
                }

                return release.Version.Major > 0 ? release : null;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to process table row: {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// Marks the latest release in the collection based on availability date
        /// </summary>
        private static void MarkLatestRelease(WindowsReleaseInfo releaseInfo)
        {
            if (releaseInfo?.Releases == null || releaseInfo.Releases.Length == 0)
                return;

            // Find the latest release by availability date
            var latestRelease = releaseInfo.Releases.OrderByDescending(r => r.AvailabilityDate).FirstOrDefault();

            if (latestRelease != null)
            {
                // Mark all releases as not latest first
                foreach (var release in releaseInfo.Releases)
                {
                    release.IsLatest = false;
                }

                // Mark the latest one
                latestRelease.IsLatest = true;
            }
        }

        private void LogVerbose(string message)
        {
            // Only log verbose messages directly since they don't cause threading issues
            if (_cmdlet != null)
            {
                try
                {
                    LoggingService.WriteVerbose(_cmdlet, message);
                }
                catch
                {
                    // Ignore threading issues with verbose logging
                }
            }
        }

        private void LogWarning(string message)
        {
            _warnings.Add(message);
        }

        private void LogError(string message, Exception? exception = null)
        {
            _errors.Add((message, exception));
        }

        /// <summary>
        /// Writes collected warnings and errors to the cmdlet (must be called from main thread)
        /// </summary>
        public void WriteCollectedMessages()
        {
            if (_cmdlet == null) return;

            foreach (var warning in _warnings)
            {
                LoggingService.WriteWarning(_cmdlet, "WindowsReleaseHistory", warning);
            }

            foreach (var (message, exception) in _errors)
            {
                LoggingService.WriteError(_cmdlet, "WindowsReleaseHistory", message, exception);
            }
        }
    }
}
