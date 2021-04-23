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

            Execute(option, args).Wait();
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
            var executes = args.Select(async arg =>
            {
                return await Execute(option, arg);

                // 処理をﾃﾞﾘｹﾞｰﾄ化
                //Func<bool> action = () => Execute(option, arg);

                //// 非同期実行
                //var iar = action.BeginInvoke(_ => { }, null);

                //// 処理が終わるまで待機する
                //while (!iar.IsCompleted)
                //{
                //    await Task.Delay(16);
                //}

                //// 非同期処理の結果を返却
                //return action.EndInvoke(iar);
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
            try
            {
                Util.WriteConsole(arg + ":作業開始");

                // 作業用ﾃﾞｨﾚｸﾄﾘﾊﾟｽ
                var tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(arg));
                var zip = tmp + ".zip";

                Util.DirectoryCreate(tmp);

                // 対象ﾌｧｲﾙを作業用ﾃﾞｨﾚｸﾄﾘにｺﾋﾟｰ
                var copyfiles = GetFiles(arg).Select(async (x, i) =>
                {
                    var dst = Path.Combine(tmp, string.Format("{0,0:D5}", i) + Path.GetExtension(x));
                    await Util.FileCopyAsync(x, dst);
                });
                await Task.WhenAll(copyfiles);

                if (Util.IsExecuteShukusen(option))
                {
                    if (!await Shukusen(tmp))
                    {
                        Util.WriteConsole(arg + ":Shukusenでｴﾗｰ");
                        return false;
                    }
                }

                // zip圧縮
                Util.CreateZipFromDirectory(tmp, zip);

                // 元の場所に移動
                Util.FileMove(zip, Path.Combine(Path.GetDirectoryName(arg), Path.GetFileName(zip)));

                // 作業用ﾃﾞｨﾚｸﾄﾘ削除
                Util.DirectoryDelete(tmp);

                Util.WriteConsole(arg + ":作業終了");
                return true;
            }
            catch (Exception ex)
            {
                Util.WriteConsole(arg + ex.ToString());
                return false;
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
                x = Regex.Replace(x, @"[0-9]+", m => string.Format("{0,0:D5}", int.Parse(m.Value)));
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

            var shukusen = Util.Chunk(Directory.GetFiles(dir)).Select(async x =>
            {
                var arg = string.Join(" ", x.Select(file => $"\"{file}\""));

                await AppSettings.Semaphore.WaitAsync();

                try
                {
                    Util.StartProcess(work, AppSettings.Shukusen, arg);
                    return x;
                }
                catch (Exception ex)
                {
                    Util.WriteConsole(ex.ToString());
                    return null;
                }
                finally
                {
                    AppSettings.Semaphore.Release();
                }
            });

            var results = await Task.WhenAll(shukusen);

            results.Where(x => x != null).SelectMany(x => x).AsParallel().ForAll(x =>
            {
                var dst = Path.Combine(
                    Path.GetDirectoryName(x),
                    $"s-{Util.GetFileNameWithoutExtension(x)}.jpg"
                );

                var fi = new FileInfo(dst);

                if (fi.Exists && fi.Length != 0)
                {
                    // 縮小が成功していたら元ﾌｧｲﾙを削除
                    File.Delete(x);
                }
                else
                {
                    // 縮小が失敗していて、且つ、縮小後ﾌｧｲﾙが残っていたら後ﾌｧｲﾙを削除
                    if (fi.Exists) fi.Delete();

                    // 縮小前のﾌｧｲﾙをﾘﾈｰﾑ
                    File.Move(x, dst);
                }
            });

            return results.All(x => x != null);
        }
    }
}
