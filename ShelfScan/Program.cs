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
    internal sealed class Program
    {
        /// <summary>Version of this application.</summary>
        public static string AppVersion { get; } = "0.5.0";

        static void Main(string[] args)
        {
            // Check valid number of arguments

            if (args.Length < 1)
                DisplayUsage("Folder argument is required.");

            if (args.Length > 2)
                DisplayUsage("Too many arguments.");

            string folder = args[0];
            string? mediaType = args.Length > 1 ? args[1].ToLower(CultureInfo.CurrentCulture) : null;

            if (folder == "--help" || folder == "-h" || folder == "/?")
                DisplayUsage();

            if (!Directory.Exists(folder))
                DisplayUsage($"Folder '{folder}' does not exist.");

            // Work out the media type if not specified

            if (!string.IsNullOrEmpty(mediaType))
            {
                string key = mediaType.ToLower(CultureInfo.CurrentCulture).Trim();
                if (key.Length > 2) key = key[..2];  // Normalize to first 2 letters
                switch (key)
                {
                    case "mo":  // movie(s)
                    case "fi":  // film(s)
                        mediaType = "movie";
                        break;
                    case "tv":  // tv
                    case "sh":  // show(s)
                    case "te":  // television
                        mediaType = "tv";
                        break;
                    default:    // unknown
                        DisplayUsage($"Unknown media type override '{mediaType}'. Use 'movie' or 'tv'.");
                        break;  // Not required, but keeps analyzer happy
                }
            }

            // Show the header with GPL notice
            ShowHeader(true);

            // Get all .mkv, .mp4 and .avi files recursively. In the future we'll add music.

            Console.WriteLine();
            Console.WriteLine($"Searching for content in {folder}...");

            List<string> files = [];
            try
            {
                AddMediaFiles(folder, files, [".mkv", ".mp4", ".avi"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning files: {ex.Message}");
                Environment.Exit(-1);
            }

            // If no override type defined, then try to auto-detect based on folder structure
            if (string.IsNullOrEmpty(mediaType))
                mediaType = GuessMediaType(files);

            // Now process each file

            int total = files.Count;
            int validCount = 0;
            int invalidCount = 0;
            bool isValid;

            Console.WriteLine();
            Console.WriteLine($"---------- BEGIN {mediaType.ToUpper(CultureInfo.CurrentCulture)} REPORT ----------");

            // Show some disclaimer text

            Console.WriteLine();
            Console.WriteLine("Beta notice:");
            Console.WriteLine();
            Console.WriteLine($"This tool is an early beta (v{AppVersion}). There may be mistakes in the logic.");
            Console.WriteLine("If you encounter any issues or incorrect results, please report them on GitHub.");
            Console.WriteLine();
            Console.WriteLine("Strict file format checking:");
            Console.WriteLine();
            Console.WriteLine("File format checks are very strict. A file marked as invalid in this report");
            Console.WriteLine("does not necessarily mean there is a problem with it in Plex.");
            Console.WriteLine();
            Console.WriteLine("Resources:");
            Console.WriteLine();
            Console.WriteLine("- https://support.plex.tv/articles/naming-and-organizing-your-tv-show-files/");
            Console.WriteLine("- https://support.plex.tv/articles/naming-and-organizing-your-movie-files/");
            Console.WriteLine();
            Console.WriteLine("Scan results:");

            foreach (var file in files)
            {
                if (mediaType == "movie")
                    isValid = PlexMovieVerifier.VerifyMovies(file, folder);
                else
                    isValid = PlexShowVerifier.VerifyShow(file);

                // Tally results
                if (isValid)
                    validCount++;
                else
                    invalidCount++;
            }

            Console.WriteLine();
            Console.WriteLine($"Summary:");
            Console.WriteLine();
            Console.WriteLine($"Valid files:          {validCount,6:N0}");
            Console.WriteLine($"Invalid files:        {invalidCount,6:N0}");
            Console.WriteLine($"Total files checked:  {total,6:N0}");
            float pc = (float)(validCount * 100.0) / (float)total;
            string niceMessage = pc switch
            {
                >= 100.0f => "(perfect score!)",
                >= 95.0f => "(excellent!)",
                >= 90.0f => "(great job!)",
                >= 85.0f => "(good effort)",
                _ => ""
            };
            Console.WriteLine($"Correctness:          {pc,6:N2}% {niceMessage}");

            Console.WriteLine();
            Console.WriteLine($"---------- END {mediaType.ToUpper(CultureInfo.CurrentCulture)} REPORT ----------");
            Environment.Exit(0);
        }

        /// <summary>
        /// Simple heuristic to guess if the folder contains movies or TV shows. The
        /// user can ovverride this with a command line argument.
        /// </summary>
        /// <param name="folder">Path to content</param>
        /// <param name="files">List</param>
        /// <returns></returns>
        private static string GuessMediaType(List<string> files)
        {
            // Look for SxxExx patterns in filenames to indicate TV shows
            Regex tvPattern = new(@"S\d{1,2}E\d{1,2}", RegexOptions.IgnoreCase);

            foreach (var file in files)
                if (tvPattern.IsMatch(file))
                    return "tv";

            // Default to movie
            return "movie";
        }

        /// <summary>
        /// Display the application header. If showGPL is true, also show the GPL license notice.
        /// </summary>
        /// <param name="showGPL"></param>
        private static void ShowHeader(bool showGPL = false)
        {
            Console.WriteLine($"ShelfScan v{AppVersion} - Scans a media library for Plex naming compliance.");
            if (showGPL)
            {
                Console.WriteLine("http://github.com/mrsilver76/shelfscan/");
                Console.WriteLine();
                Console.WriteLine("This program is free software: you can redistribute it and/or modify");
                Console.WriteLine("it under the terms of the GNU General Public License as published by");
                Console.WriteLine("the Free Software Foundation, either version 2 of the License, or");
                Console.WriteLine("at your option) any later version.");
            }
        }
        /// <summary>
        /// Display usage information and exit. If an error message is provided, show that too
        /// and exit with error code -1.
        /// </summary>
        /// <param name="errorMessage"></param>
        private static void DisplayUsage(string errorMessage = "")
        {
            Console.WriteLine("Usage: ShelfScan <folder> [movie|tv]");
            if (errorMessage == "")
                ShowHeader(true);
            else
                ShowHeader();
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <folder>      Folder of content to scan");
            Console.WriteLine("  [movie|tv]    (Optional) Override auto-detection to content type.");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Add all media files in the specified folder and its subfolders to the list. Ignores
        /// any "Plex Versions" folders.
        /// </summary>
        /// <param name="folder">Root folder</param>
        /// <param name="files">List to add files to</param>
        /// <param name="extensions">Extensions to include</param>
        private static void AddMediaFiles(string folder, List<string> files, IEnumerable<string> extensions)
        {
            foreach (var dir in Directory.EnumerateDirectories(folder))
            {
                // Skip Plex Versions and all subfolders
                if (Path.GetFileName(dir).Equals("Plex Versions", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddMediaFiles(dir, files, extensions);
            }

            // Collect files in this folder
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var ext = Path.GetExtension(file);
                if (!string.IsNullOrEmpty(ext) && extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    files.Add(file);
            }
        }
    }
}
