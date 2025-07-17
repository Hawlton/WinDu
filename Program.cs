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

        List<FileInfo> all_files = new List<FileInfo>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(start_path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    all_files.Add(new FileInfo(file));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting file info for '{file}': {ex.Message}");
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"Access Denied: Cannot enumerate file system from thist start point {start_path} ");
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        var unique_paths = new HashSet<string>();
        foreach(var item in all_directories)
        {
            unique_paths.Add(item.path);
        }

        foreach(string path in unique_paths.OrderBy(p => p.Length))
        {
            long current_total_size = 0;
            current_total_size = all_files.Where(f => f.FullName.StartsWith(path, StringComparison.OrdinalIgnoreCase)).Sum(f =>  f.Length);
            long min_byte_size = min_size_gb * 1024L * 1024L * 1024L;
            if(current_total_size >= min_byte_size)
            {
                results.Add(new DirectoryInfoItem
                {
                    path = path,
                    byte_size = current_total_size,
                });
            }
        }

        var sorted_results = results.OrderByDescending(d => d.byte_size);
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

        public string GetHumanReadableSize
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