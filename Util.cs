using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp27
{
    public static class Util
    {
        [DllImport("kernel32.dll")]
        private static extern int GetShortPathName(string longPath, StringBuilder shortPathBuffer, int bufferSize);

        private static string GetShortPathName(string longpath)
        {
            const int bufferSize = 260;
            var sb = new StringBuilder(bufferSize);

            GetShortPathName(longpath, sb, bufferSize);

            if (0 < sb.Length)
            {
                return sb.ToString();
            }
            else
            {
                return longpath;
            }
        }

        public static string GetExtension(string value)
        {
            try
            {
                using (var bitmap = new Bitmap(value))
                {
                    var result = decoders.FirstOrDefault(ici => ici.FormatID == bitmap.RawFormat.Guid);
                    if (result != null)
                    {
                        return result.FilenameExtension.Split(';').First().Substring(1);
                    }
                }
            }
            catch
            {

            }
            return null;
        }
        private static ImageCodecInfo[] decoders = ImageCodecInfo.GetImageDecoders();

        public static void FileMove(string src, string dst)
        {
            FileDelete(dst);

            File.Move(GetShortPathName(src), GetShortPathName(dst));
        }

        public static void FileDelete(string src)
        {
            src = GetShortPathName(src);

            if (File.Exists(src)) File.Delete(src);
        }

        public static async Task FileCopyAsync(string src, string dst)
        {
            const FileOptions fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
            const int bufferSize = 4096;

            var cts = new CancellationTokenSource();
            using (var sStream = new FileStream(GetShortPathName(src), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions))
            using (var dStream = new FileStream(GetShortPathName(dst), FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, fileOptions))
            {
                await sStream.CopyToAsync(dStream, bufferSize, cts.Token).ConfigureAwait(false);
            }

            //await WaitAsync(() => File.Copy(GetShortPathName(src), GetShortPathName(dst)));
        }

        public static void DirectoryCreate(string dir)
        {
            if (Directory.Exists(dir)) DirectoryDelete(dir);

            Directory.CreateDirectory(dir);
        }

        public static void DirectoryDelete(string src)
        {
            var info = new DirectoryInfo(GetShortPathName(src));

            foreach (var file in info.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if (file.Attributes.HasFlag(FileAttributes.Directory))
                {
                    file.Attributes = FileAttributes.Directory;
                }
                else
                {
                    file.Attributes = FileAttributes.Normal;
                }
            }

            info.Delete(true);
        }

        public static string GetFileNameWithoutExtension(string file)
        {
            file = file.Contains(@"\") ? Path.GetFileName(file) : file;
            return Regex.Replace(file, @"\.[a-zA-Z]{1,5}$", p => "");
        }

        public static bool IsExecuteShukusen(int option)
        {
            return option == 0;
        }

        public static IEnumerable<string[]> Chunk(IEnumerable<string> source)
        {
            var target = new List<string>();

            foreach (var file in source)
            {
                if (8000 < target.Sum(a => a.Length + 3))
                {
                    // ｺﾏﾝﾄﾞﾗｲﾝ引数の上限は8192文字なので、それを超えない範囲で配列を分割する。
                    yield return target.ToArray();

                    target.Clear();
                }

                target.Add(file);
            }

            if (target.Any())
            {
                yield return target.ToArray();
            }
        }

        public static async Task<bool> StartProcess(string work, string file, string argument)
        {
            _sema = _sema ?? new SemaphoreSlim(1, AppSettings.Lock);

            try
            {
                await _sema.WaitAsync();

                var info = new ProcessStartInfo();

                info.WorkingDirectory = work;
                info.FileName = file;
                info.Arguments = argument;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.ErrorDialog = true;
                info.RedirectStandardError = true;

                using (var process = Process.Start(info))
                {
                    process.WaitForExit();
                }
                return true;
            }
            catch (Exception ex)
            {
                Util.WriteConsole(ex.ToString());
                return false;
            }
            finally
            {
                _sema.Release();
            }
        }
        public static SemaphoreSlim _sema  = new SemaphoreSlim(AppSettings.Lock, AppSettings.Lock);

        public static void WriteConsole(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("MM.dd HH:mm:ss.fff")}: {message}");
        }

        public static void CreateZipFromDirectory(string src, string dst, CompressionLevel level = CompressionLevel.Optimal, bool includeBaseDirectory = true)
        {
            ZipFile.CreateFromDirectory(src, dst, level, includeBaseDirectory);
        }

        public static async Task WaitAsync(Action action)
        {
            var iar = action.BeginInvoke(tmp => { }, null);
            await WaitAsync(iar);
            action.EndInvoke(iar);
        }

        public static Task<bool> WaitAsync(IAsyncResult iar)
        {
            return WaitAsync(iar, TimeSpan.MaxValue);
        }

        public static async Task<bool> WaitAsync(IAsyncResult iar, TimeSpan ts, CancellationTokenSource cts = null)
        {
            _stopwatch.Restart();
            while (!iar.IsCompleted && _stopwatch.Elapsed < ts && (cts == null || !cts.IsCancellationRequested))
            {
                await Task.Delay(16);
            }
            _stopwatch.Stop();
            return _stopwatch.Elapsed <= ts;
        }
        private static Stopwatch _stopwatch = new Stopwatch();

    }
}
