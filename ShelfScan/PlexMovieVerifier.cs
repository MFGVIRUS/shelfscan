/*
 * ShelfScan - Scans a media library for Plex naming compliance.
 * http://github.com/mrsilver76/shelfscan/
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *  
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this Options.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Globalization;
using System.Text.RegularExpressions;

namespace ShelfScan
{
    public sealed class PlexMovieVerifier
    {
        // Extras suffixes for inline local extras
        private static readonly string[] InlineExtraSuffixes =
        [
            "-behindthescenes", "-deleted", "-featurette", "-interview",
            "-scene", "-short", "-trailer", "-other"
        ];

        // Valid subdirectories for extras
        private static readonly string[] ExtraSubdirectories =
        [
            "Behind The Scenes", "Deleted Scenes", "Featurettes", "Interviews",
            "Scenes", "Shorts", "Trailers", "Other"
        ];

        /// <summary>
        /// Verify a movie file against Plex naming rules.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// 

        public static bool VerifyMovies(string filePath, string rootFolder)
        {
            string fileName = Path.GetFileName(filePath);
            string folderPath = Path.GetDirectoryName(filePath)!;
            string parentFolder = Path.GetFileName(folderPath);

            string cleanFolderName = StripBrackets(parentFolder);

            // 1. Check for extras in subdirectory
            if (Array.Exists(ExtraSubdirectories, s => s.Equals(cleanFolderName, StringComparison.OrdinalIgnoreCase)))
                return true;

            // 2. Check for inline extras in filename
            foreach (var extra in InlineExtraSuffixes)
                if (fileName.EndsWith(extra + Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
                    return true;

            // 3. Validate { } blocks
            var braceMatches = Regex.Matches(fileName, @"\{.*?\}");
            foreach (Match match in braceMatches)
            {
                string content = match.Value;
                if (!content.StartsWith("{edition-", StringComparison.OrdinalIgnoreCase) &&
                    !content.StartsWith("{imdb-", StringComparison.OrdinalIgnoreCase) &&
                    !content.StartsWith("{tmdb-", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"\n{filePath}\n  Invalid block '{content}'. Must be edition-, imdb-, or tmdb-");
                    return false;
                }
            }

            // 4. Strip { } and [ ] for core validation only
            string coreName = Regex.Replace(Path.GetFileNameWithoutExtension(fileName), @"(\{.*?\}|\[.*?\])", "").Trim();

            // 5. Validate core name: title, year, optional split (only valid CD/disc/part)
            string corePattern = @"^(?<title>.+) \((?<year>\d{4})\)\s*( - (?<split>cd\d+|disc\d+|disk\d+|dvd\d+|part\d+|pt\d+))?$";
            Regex coreRegex = new(corePattern, RegexOptions.IgnoreCase);
            var coreMatch = coreRegex.Match(coreName);

            if (!coreMatch.Success)
            {
                Console.WriteLine($"\n{filePath}\n  Invalid naming format. Expected 'Movie Name (YYYY){{optional split}}'");
                return false;
            }

            string title = coreMatch.Groups["title"].Value.Trim();
            string yearStr = coreMatch.Groups["year"].Value;

            // 6. Validate year
            if (!int.TryParse(yearStr, out int year) || year < 1900 || year > DateTime.Now.Year + 1)
            {
                Console.WriteLine($"\n{filePath}\n  Invalid year '{yearStr}'. Must be between 1900 and {DateTime.Now.Year + 1}");
                return false;
            }

            // 7. Validate folder consistency if not in root
            if (!string.Equals(Path.GetFullPath(folderPath).TrimEnd('\\'),
                               Path.GetFullPath(rootFolder).TrimEnd('\\'),
                               StringComparison.OrdinalIgnoreCase))
            {
                // Remove optional split from filename for folder comparison
                string fileBaseName = Regex.Replace(Path.GetFileNameWithoutExtension(fileName),
                                                    @" - (cd\d+|disc\d+|disk\d+|dvd\d+|part\d+|pt\d+)$",
                                                    "", RegexOptions.IgnoreCase);

                if (!fileBaseName.Equals(parentFolder, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"\n{filePath}\n  Folder name '{parentFolder}' does not match filename '{fileBaseName}'");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Removes [bracketed] sections from a name. These are ignored in Plex naming.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string StripBrackets(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            return Regex.Replace(name, @"\s*\[[^\]]*\]", ""); // remove [ ... ] blocks
        }
    }
}
