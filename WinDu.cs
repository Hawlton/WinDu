using System;
using System.IO;

public class WinDu
{
    static void Main(string[] args)
    {
        string start_path = Path.GetPathRoot(Environment.SystemDirectory);
        long min_size_gb = 1;
        int max_depth = -1;
        string output_path = String.Empty;
        if(args.Length == 0)
        {
            ShowHelp();
            return;
        }
 
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-path":
                case "-p":
                    if(i + 1 < args.Length)
                    {
                        start_path = args[++i];
                    }
                    break;
                case "-minsize":
                case "-m":
                    if(i + 1 <args.Length && long.TryParse(args[++i], out long size))
                    {
                        min_size_gb = size;
                    }
                    break;
                case "-maxdepth":
                case "-d":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int depth))
                    {
                        max_depth = depth;
                    }
                    break;
                case "-output":
                case "-o":
                    if(i + 1 < args.Length)
                    {
                        output_path = args[++i];
                    }
                    break;
                case "-help":
                case "-h":
                case "/h":
                case "/?":
                case "-?":
                    ShowHelp();
                    return;

            }
        }

        if (!Directory.Exists(start_path))
        {
            Console.WriteLine($"[ERROR]: The Specified path '{start_path}' could not be found");
            ShowHelp();
            return;
        }

        Console.WriteLine($"Starting disk usage scan for: {start_path}");
        Console.WriteLine($"Reporting folders larger than: {min_size_gb} GB");
        if (max_depth != -1) Console.WriteLine($"Scanning with a max depth of: {max_depth}");
        if(!String.IsNullOrEmpty(output_path))
        {
            Console.WriteLine($"Results will be saved to: '{output_path}");
            try
            {
                if (File.Exists(output_path)) File.Delete(output_path);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Could not clear previous results file '{output_path}'. Details: {ex.Message}");
            }
        }

        List<DirectoryInfoItem> all_directories = new List<DirectoryInfoItem>();
        Queue<Tuple<string, int>> remaining_directories = new Queue<Tuple<string, int>>();
        remaining_directories.Enqueue(Tuple.Create(start_path, 0));

        while(remaining_directories.Count > 0)
        {
            var current_tuple = remaining_directories.Dequeue();
            string current_path = current_tuple.Item1;
            int current_depth = current_tuple.Item2;
            if(max_depth != -1 && current_depth > max_depth)
            {
                continue;
            }
            Console.WriteLine($"Scanning: {current_path} Depth: {current_depth}");
            long current_dir_size = 0;
            IEnumerable<string> sub_directories = null;
            IEnumerable<string> files = null;

            try
            {
                files = Directory.EnumerateFiles(current_path);
                foreach(string item in files)
                {
                    try
                    {
                        current_dir_size += new FileInfo(item).Length;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"Access Denied for file: {item}");
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine("File Not found");
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                sub_directories = Directory.EnumerateDirectories(current_path);
                foreach(string item in sub_directories)
                {
                    if(max_depth == -1 || current_depth + 1 <= max_depth)
                    {
                        remaining_directories.Enqueue(Tuple.Create(item, current_depth + 1));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Access Denied for directory: {current_path}");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            all_directories.Add(new DirectoryInfoItem
            {
                path = current_path,
                byte_size = current_dir_size,
                depth = current_depth,
            });
        }

        List<DirectoryInfoItem> results = new List<DirectoryInfoItem>();
        Console.WriteLine("Aggregating Total Sizes...");
        CalculateDirectorySizesRecursive(start_path, 0, max_depth, results, min_size_gb);

        Console.WriteLine("---SCAN COMPLETE---");
        Console.WriteLine("Results (sorted by size):");

        List<string> output_lines = new List<string>();
        output_lines.Add("---Disk Usage Report---");
        output_lines.Add($"Scan path: {start_path}");
        output_lines.Add($"Size Threshold: {min_size_gb}");
        output_lines.Add($"Max Depth: {(max_depth == -1 ? "Infinite" : max_depth.ToString())}");
        output_lines.Add("---------------------------------------------------------------------");
        output_lines.Add(string.Format("{0, -15} {1} ", "size", "Path"));
        output_lines.Add("---------------------------------------------------------------------");


        foreach(var item in results.OrderByDescending(x => x.byte_size))
        {
            output_lines.Add($"{item.byte_size} | {item.path}");
        }

        foreach(string line in output_lines)
        {
            Console.WriteLine(line);
        }

        if (!string.IsNullOrEmpty(output_path))
        {
            try
            {
                File.AppendAllLines(output_path, output_lines);
                Console.WriteLine($"Report saved to: {output_path}");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }

    private static long CalculateDirectorySizesRecursive(string currentPath, int currentDepth, int maxDepth, List<DirectoryInfoItem> results, long minSizeInBytes)
    {
        long totalSizeForCurrentPath = 0;

        if (maxDepth != -1 && currentDepth > maxDepth)
        {
            return 0;
        }

        DirectoryInfo currentDirInfo = new DirectoryInfo(currentPath);

        // Check for reparse points (junctions, symbolic links) to prevent infinite loops
        // This check must be done on the DirectoryInfo object itself.
        if ((currentDirInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
        {
            Console.WriteLine($"Skipping Reparse Point (Potential Loop): {currentPath}");
            return 0; 
        }

        try
        {
            IEnumerable<string> files = null;
            try
            {
                files = Directory.EnumerateFiles(currentPath, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Access Denied (Files Enumeration): {currentPath}");
                files = Enumerable.Empty<string>(); 
            }
            catch (IOException ex) 
            {
                Console.WriteLine($"IO Error (Files Enumeration): {currentPath} - {ex.Message}");
                files = Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating files in '{currentPath}': {ex.Message}");
                files = Enumerable.Empty<string>();
            }

            foreach (string file in files)
            {
                try
                {
                    totalSizeForCurrentPath += new FileInfo(file).Length;
                }
                catch (UnauthorizedAccessException) { }
                catch (FileNotFoundException) { }
                catch (IOException) { }
                catch (Exception ex) { Console.WriteLine($"Error processing file '{file}': {ex.Message}"); }
            }

            // Recursively sum subdirectories
            IEnumerable<string> subDirs = null;
            try
            {
                subDirs = Directory.EnumerateDirectories(currentPath, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Access Denied (Subdirectories Enumeration): {currentPath}");
                subDirs = Enumerable.Empty<string>(); 
            }
            catch (IOException ex) 
            {
                Console.WriteLine($"IO Error (Subdirectories Enumeration): {currentPath} - {ex.Message}");
                subDirs = Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating subdirectories in '{currentPath}': {ex.Message}");
                subDirs = Enumerable.Empty<string>();
            }

            foreach (string subDir in subDirs)
            {
                totalSizeForCurrentPath += CalculateDirectorySizesRecursive(subDir, currentDepth + 1, maxDepth, results, minSizeInBytes);
            }

            if (totalSizeForCurrentPath >= minSizeInBytes)
            {
                if (maxDepth == -1 || currentDepth <= maxDepth)
                {
                    results.Add(new DirectoryInfoItem
                    {
                        path = currentPath,
                        byte_size = totalSizeForCurrentPath
                    });
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"Access Denied (Directory Traversal): {currentPath}");
            return 0;
        }
        catch (PathTooLongException)
        {
            Console.WriteLine($"Path Too Long (Directory Traversal): {currentPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Error accessing directory '{currentPath}': {ex.Message}");
            return 0;
        }

        return totalSizeForCurrentPath;
    }

    public static void ShowHelp()
    {
        Console.WriteLine("\nUsage: DiskUsage.exe [-p <path>] [-m <minSizeGB>] [-d <maxDepth>] [-o <outputFilePath>] [-h]");
        Console.WriteLine("  -p, -path <path>        : Starting directory to scan (e.g., C:\\). Default is C:\\.");
        Console.WriteLine("  -m, -minsizegb <size>   : Minimum size in GB for a folder to be reported. Default is 1 GB.");
        Console.WriteLine("  -d, -maxdepth <depth>   : Maximum directory depth to scan. -1 for infinite (default).");
        Console.WriteLine("  -o, -output <filePath>  : Path to a file to save the report. (e.g., C:\\Reports\\du_report.txt)");
        Console.WriteLine("  -h, -help, /?           : Show this help message.");
        Console.WriteLine("\nExample: DiskUsage.exe -p C:\\ -m 0.1 -d 3 -o C:\\Temp\\MyDiskReport.txt");
        Console.WriteLine("  (Scans C:\\, reports folders > 0.1GB, up to 3 levels deep, saves to C:\\Temp\\MyDiskReport.txt)");
    }

    public class DirectoryInfoItem
    {
        public string path { get; set; }
        public long byte_size { get; set; }
        public int depth { get; set; }

        public string human_readable_size
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = byte_size;
                int order = 0;
                while(len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return string.Format("{0.0.##} {1}", len, sizes[order]);
            }
        }
    }
}