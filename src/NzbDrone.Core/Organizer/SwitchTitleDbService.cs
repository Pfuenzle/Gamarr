using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Organizer
{
    public interface ISwitchTitleDbService
    {
        SwitchTitleDbMatch FindBaseByTitles(IEnumerable<string> titles);
        SwitchTitleDbMatch FindDlcByTitles(IEnumerable<string> titles);
        long? GetLatestVersion(string titleId);
    }

    public class SwitchTitleDbService : ISwitchTitleDbService
    {
        private const string GitHubContentsUrl = "https://api.github.com/repos/blawar/titledb/contents";
        private const string VersionsUrl = "https://raw.githubusercontent.com/blawar/titledb/master/versions.txt";
        private static readonly Regex LocaleFileRegex = new Regex(@"^[A-Z]{2}\.[a-z]{2}\.json$", RegexOptions.Compiled);
        private static readonly IReadOnlyList<string> PreferredLocaleFiles = new[]
        {
            "US.en.json",
            "GB.en.json",
            "JP.ja.json",
            "JP.en.json",
            "DE.de.json",
            "FR.fr.json",
            "ES.es.json",
            "IT.it.json",
            "KR.ko.json",
            "CN.zh.json",
            "HK.zh.json",
            "TW.zh.json",
            "BR.pt.json",
            "MX.es.json"
        };

        private readonly IHttpClient _httpClient;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, Dictionary<string, List<SwitchTitleDbMatch>>> _titlesByLocaleFile = new Dictionary<string, Dictionary<string, List<SwitchTitleDbMatch>>>(StringComparer.OrdinalIgnoreCase);
        private List<string> _localeFiles;
        private Dictionary<string, long> _versionsByTitleId;

        public SwitchTitleDbService(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public SwitchTitleDbMatch FindBaseByTitles(IEnumerable<string> titles)
        {
            EnsureLoaded();
            return Find(titles, match => match.TitleId.EndsWith("000", StringComparison.OrdinalIgnoreCase));
        }

        public SwitchTitleDbMatch FindDlcByTitles(IEnumerable<string> titles)
        {
            EnsureLoaded();
            return Find(titles, match => !match.TitleId.EndsWith("000", StringComparison.OrdinalIgnoreCase) && !match.TitleId.EndsWith("800", StringComparison.OrdinalIgnoreCase));
        }

        public long? GetLatestVersion(string titleId)
        {
            EnsureLoaded();

            if (string.IsNullOrWhiteSpace(titleId))
            {
                return null;
            }

            return _versionsByTitleId.GetValueOrDefault(titleId.ToUpperInvariant());
        }

        private SwitchTitleDbMatch Find(IEnumerable<string> titles, Func<SwitchTitleDbMatch, bool> predicate)
        {
            var cleanTitles = titles?
                .Where(title => title.IsNotNullOrWhiteSpace())
                .Select(title => title.CleanGameTitle())
                .Distinct()
                .ToList();

            if (cleanTitles == null || cleanTitles.Count == 0)
            {
                return null;
            }

            foreach (var localeFile in _localeFiles)
            {
                var titlesByCleanName = GetTitlesForLocaleFile(localeFile);

                foreach (var cleanTitle in cleanTitles)
                {
                    if (!titlesByCleanName.TryGetValue(cleanTitle, out var matches))
                    {
                        continue;
                    }

                    var match = matches.FirstOrDefault(predicate);

                    if (match != null)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private void EnsureLoaded()
        {
            if (_localeFiles != null && _versionsByTitleId != null)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_localeFiles != null && _versionsByTitleId != null)
                {
                    return;
                }

                _localeFiles = LoadLocaleFiles();
                _versionsByTitleId = LoadVersions();
            }
        }

        private List<string> LoadLocaleFiles()
        {
            using var document = JsonDocument.Parse(_httpClient.Get(new HttpRequest(GitHubContentsUrl)).Content);

            var discovered = document.RootElement
                .EnumerateArray()
                .Select(element => element.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => name.IsNotNullOrWhiteSpace() && LocaleFileRegex.IsMatch(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return PreferredLocaleFiles
                .Concat(discovered.Where(name => !PreferredLocaleFiles.Contains(name, StringComparer.OrdinalIgnoreCase)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        private Dictionary<string, List<SwitchTitleDbMatch>> GetTitlesForLocaleFile(string localeFile)
        {
            if (_titlesByLocaleFile.TryGetValue(localeFile, out var cached))
            {
                return cached;
            }

            lock (_syncRoot)
            {
                if (_titlesByLocaleFile.TryGetValue(localeFile, out cached))
                {
                    return cached;
                }

                cached = LoadTitles(localeFile);
                _titlesByLocaleFile[localeFile] = cached;
                return cached;
            }
        }

        private Dictionary<string, List<SwitchTitleDbMatch>> LoadTitles(string localeFile)
        {
            var titlesByCleanName = new Dictionary<string, List<SwitchTitleDbMatch>>();
            var titleUrl = $"https://raw.githubusercontent.com/blawar/titledb/master/{localeFile}";
            var content = _httpClient.Get(new HttpRequest(titleUrl)).Content;

            using var document = JsonDocument.Parse(content);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var titleElement = property.Value;

                if (!titleElement.TryGetProperty("id", out var idElement) || !titleElement.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var titleId = idElement.GetString();
                var name = nameElement.GetString();

                if (string.IsNullOrWhiteSpace(titleId) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var cleanTitle = name.CleanGameTitle();

                if (!titlesByCleanName.TryGetValue(cleanTitle, out var matches))
                {
                    matches = new List<SwitchTitleDbMatch>();
                    titlesByCleanName[cleanTitle] = matches;
                }

                if (matches.Any(existing => existing.TitleId == titleId))
                {
                    continue;
                }

                matches.Add(new SwitchTitleDbMatch(name, titleId));
            }

            return titlesByCleanName;
        }

        private Dictionary<string, long> LoadVersions()
        {
            var versions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var content = _httpClient.Get(new HttpRequest(VersionsUrl)).Content;

            using var reader = new StringReader(content);

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("id|", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = line.Split('|');

                if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    continue;
                }

                if (!long.TryParse(parts[2], out var version))
                {
                    version = 0;
                }

                versions[parts[0].ToUpperInvariant()] = version;
            }

            return versions;
        }
    }

    public class SwitchTitleDbMatch
    {
        public SwitchTitleDbMatch(string title, string titleId)
        {
            Title = title;
            TitleId = titleId;
        }

        public string Title { get; }
        public string TitleId { get; }
    }

    public class SwitchTitleDbParsedFileName
    {
        private static readonly Regex FilePattern = new Regex(@"\[(?<titleId>0100[0-9A-Fa-f]{12})\]\s*\[v(?<version>\d+)\]", RegexOptions.Compiled);

        public static SwitchTitleDbParsedFileName Parse(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var match = FilePattern.Match(fileName);

            if (!match.Success || !long.TryParse(match.Groups["version"].Value, out var version))
            {
                return null;
            }

            return new SwitchTitleDbParsedFileName(match.Groups["titleId"].Value.ToUpperInvariant(), version);
        }

        public SwitchTitleDbParsedFileName(string titleId, long version)
        {
            TitleId = titleId;
            Version = version;
        }

        public string TitleId { get; }
        public long Version { get; }
    }
}
