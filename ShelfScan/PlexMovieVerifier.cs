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
        public static bool VerifyMovies(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string parentFolder = Path.GetFileName(Path.GetDirectoryName(filePath)!);
            string folderPath = Path.GetDirectoryName(filePath)!;
            bool isValid = true;

            // Strip out [brackets]
            string cleanFileName = StripBrackets(fileName);
            string cleanFolderName = StripBrackets(parentFolder);

            // 1. Check for extras in subdirectory
            string grandParentFolder = Path.GetFileName(Path.GetDirectoryName(folderPath)!);
            if (Array.Exists(ExtraSubdirectories, s => s.Equals(cleanFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                // File is inside an extras folder → ignore
                return true;
            }

            // 2. Check for inline extras in filename
            foreach (var extra in InlineExtraSuffixes)
            {
                if (cleanFileName.EndsWith(extra + Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Inline extra → ignore
                }
            }

            // 3. Regex for main movie file
            string pattern = @"^(?<title>.+?) \((?<year>\d{4})( \{(imdb|tmdb)-[^\}]+\})?\)( (?<edition>\{edition-[^\}]+\}))?(?<split>-(cd|disc|disk|dvd|part|pt)\d+)?\.(mkv|mp4)$";
            Regex regex = new(pattern, RegexOptions.IgnoreCase);

            if (!regex.IsMatch(cleanFileName))
            {
                Console.WriteLine($"\n{filePath}\n  Invalid naming format. Expected 'Movie Name (YYYY){{id}}{{edition}}{{split}}.ext'");
                return false;
            }

            Match match = regex.Match(cleanFileName);
            string title = match.Groups["title"].Value.Trim();
            string yearStr = match.Groups["year"].Value;
            string edition = match.Groups["edition"].Success ? match.Groups["edition"].Value : "";
            string split = match.Groups["split"].Success ? match.Groups["split"].Value : "";

            // 4. Validate year
            if (!int.TryParse(yearStr, out int year) || year < 1900 || year > DateTime.Now.Year + 1)
            {
                Console.WriteLine($"\n{filePath}\n  Invalid year '{yearStr}'. Must be between 1900 and {DateTime.Now.Year + 1}.");
                isValid = false;
            }

            // 5. Validate folder consistency
            string expectedFolder = $"{title} ({year})";
            if (!string.IsNullOrEmpty(edition))
                expectedFolder += " " + edition;

            if (!expectedFolder.Equals(cleanFolderName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"\n{filePath}\n  Folder name '{parentFolder}' does not match expected '{expectedFolder}'");
                isValid = false;
            }

            // 6. Split suffix rules for stand-alone files
            if (string.IsNullOrEmpty(parentFolder) && !string.IsNullOrEmpty(split))
            {
                Console.WriteLine($"\n{filePath}\n  Split suffix used in a stand-alone file without folder.");
                isValid = false;
            }

            return isValid;
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
