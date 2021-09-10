using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp27
{
    class Program
    {
        static void Main(string[] args)
        {
            // ｵﾌﾟｼｮﾝを選択
            Console.WriteLine("起動ｵﾌﾟｼｮﾝを選択してください。");
            Console.WriteLine("0: 全て実行する。");
            Console.WriteLine("1: 画像縮小をｽｷｯﾌﾟする。");
            Console.WriteLine($"ﾃﾞﾌｫﾙﾄ: {AppSettings.Option}");

            // ｵﾌﾟｼｮﾝを選択
            var option = GetOption(Console.ReadLine());

            var task = Execute(option, args);

            task.Wait();

            if (task.Exception != null)
            {
                Util.WriteConsole(task.Exception.ToString());
                // ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
                Console.ReadLine();
            }
        }

        private static int GetOption(string line)
        {
            switch (line)
            {
                case "0":
                case "1":
                    return int.Parse(line);
                default:
                    return AppSettings.Option;
            }
        }

        private static async Task Execute(int option, string[] args)
        {
            var executes = args.AsParallel().Select(async arg =>
            {
                return await Execute(option, arg);
            });

            // 実行ﾊﾟﾗﾒｰﾀに対して処理実行
            var results = await Task.WhenAll(executes);

            if (results.Contains(false))
            {
                // ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
                Console.ReadLine();
            }
        }

        private static async Task<bool> Execute(int option, string arg)
        {
            // 作業用ﾃﾞｨﾚｸﾄﾘﾊﾟｽ
            var tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(arg));
            var zip = tmp + ".zip";

            try
            {
                Util.WriteConsole("***** 開始:" + arg);

                Util.DirectoryCreate(tmp);

                Util.WriteConsole("終了(作業用ﾃﾞｨﾚｸﾄﾘ作成):" + arg);

                // 対象ﾌｧｲﾙを作業用ﾃﾞｨﾚｸﾄﾘにｺﾋﾟｰ
                var copyfiles = GetFiles(arg).Select(async (x, i) =>
                {
                    var dst = Path.Combine(tmp, string.Format("{0,0:D8}", i) + Path.GetExtension(x));
                    await Util.FileCopyAsync(x, dst);
                });
                await Task.WhenAll(copyfiles);

                Util.WriteConsole("終了(作業ﾌｧｲﾙｺﾋﾟｰ):" + arg);

                if (Util.IsExecuteShukusen(option))
                {
                    if (!await Shukusen(tmp))
                    {
                        Util.WriteConsole("異常(Shukusen):" + arg);
                        return false;
                    }
                    Util.WriteConsole("終了(Shukusen):" + arg);
                }

                // zip圧縮
                Util.CreateZipFromDirectory(tmp, zip);

                Util.WriteConsole("終了(ZIP):" + arg);

                // 元の場所に移動
                Util.FileMove(zip, Path.Combine(Path.GetDirectoryName(arg), Path.GetFileName(zip)));

                Util.WriteConsole("終了(移動):" + arg);

                // 作業用ﾃﾞｨﾚｸﾄﾘ削除
                Util.DirectoryDelete(tmp);

                // 元のﾃﾞｨﾚｸﾄﾘ削除
                Util.DirectoryDelete(arg);

                Util.WriteConsole("***** 終了:" + arg);

                return true;
            }
            catch (Exception ex)
            {
                Util.WriteConsole(arg + ex.ToString());
                return false;
            }
            finally
            {
                // 作業用ﾃﾞｨﾚｸﾄﾘ削除
                Util.DirectoryDelete(tmp);
            }
        }

        private static IEnumerable<string> GetFiles(string dir)
        {
            foreach (var f in OrderBy(Directory.GetFiles(dir)))
            {
                var tmp = OrganizeExtension(f);

                if (!IsTargetFile(tmp))
                {
                    continue;
                }

                yield return tmp;
            }

            foreach (var c in OrderBy(Directory.GetDirectories(dir)))
            {
                if (!IsTargetDirectory(c))
                {
                    continue;
                }

                foreach (var f in GetFiles(c))
                {
                    yield return f;
                }
            }
        }

        private static IEnumerable<string> OrderBy(string[] bases)
        {
            return bases.OrderBy(x =>
            {
                x = Regex.Replace(x, @"^[a-zA-Z]*cover[a-zA-Z]*", m => "!!!");
                x = Regex.Replace(x, @"[0-9]{1,8}", m => string.Format("{0,0:D8}", long.Parse(m.Value)));
                return x;
            });
        }

        private static string OrganizeExtension(string file)
        {
            var src = Path.GetExtension(file);
            var dst = Util.GetExtension(file);

            if (dst == null)
            {
                return null;
            }

            if (src.ToLower() != dst.ToLower())
            {
                var tmp = Path.Combine(Path.GetDirectoryName(file), $"{Util.GetFileNameWithoutExtension(file)}{dst}");
                Util.FileMove(file, tmp);
                return tmp;
            }
            else
            {
                return file;
            }

        }

        private static bool IsTargetFile(string file)
        {
            if (file == null)
            {
                return false;
            }

            if (AppSettings.IgnoreFiles.Any(x => x == Path.GetFileName(file)))
            {
                return false;
            }

            return true;
        }

        private static bool IsTargetDirectory(string dir)
        {
            if (AppSettings.IgnoreDirectories.Any(x => x == Path.GetFileName(dir)))
            {
                return false;
            }

            return true;
        }

        private static async Task<bool> Shukusen(string dir)
        {
            // 作業ﾃﾞｨﾚｸﾄﾘを取得
            var work = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

            var shukusen = Util.Chunk(Directory.GetFiles(dir)).AsParallel().Select(async x =>
            {
                var arg = string.Join(" ", x.Select(file => $"\"{file}\""));

                if (!await Util.StartProcess(work, AppSettings.Shukusen, arg))
                {
                    return false;
                }

                x.AsParallel().ForAll(y =>
                {
                    var dst = Path.Combine(
                        Path.GetDirectoryName(y),
                        $"s-{Util.GetFileNameWithoutExtension(y)}.jpg"
                    );

                    var fi = new FileInfo(dst);

                    if (fi.Exists && fi.Length != 0)
                    {
                        // 縮小が成功していたら元ﾌｧｲﾙを削除
                        File.Delete(y);
                    }
                    else
                    {
                        // 縮小が失敗していて、且つ、縮小後ﾌｧｲﾙが残っていたら後ﾌｧｲﾙを削除
                        if (fi.Exists) fi.Delete();

                        // 縮小前のﾌｧｲﾙをﾘﾈｰﾑ
                        File.Move(y, dst);
                    }
                });

                return true;
            });

            var results = await Task.WhenAll(shukusen);

            return results.All(x => x);
        }
    }
}
