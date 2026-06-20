using System;
using System.IO;
using System.IO.Compression;
using DiscUtils.Iso9660; // 必要なライブラリのusing

// csファイルを統合するには、UI制御担当のMainWindow.xaml.csと同じ名前空間にする必要がある
namespace MySimpleISOBuilder
{
    // 「partial」をつけることで、元のMainWindow.xaml.csと合体できる
    public partial class MainWindow : System.Windows.Window
    {
        /*
        private string GetSaveZipPath(string outputPath)
        {
            // D&Dされた「今の瞬間」の日付を取得する
            string today = DateTime.Now.ToString("yyyyMMdd");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // 例：C:\Users\...\Desktop\Backup_20260517.zip みたいな文字列を作って返す
            return Path.Combine(desktopPath, $"Backup_{today}.zip");
        }
        */

        // 保存パスを取得するメソッドを、拡張子を引数に取る汎用的なものに変更
        private string GetSaveName(string extension)
        {
            // D&Dされた「今の瞬間」の日付を取得する
            string today = DateTime.Now.ToString("yyyyMMdd");

            // Backup_20260517.zip や Backup_20260517.iso みたいな文字列を作って返す
            // FIXME: zip作成時はフルパスが必要な一方、iso作成時はファイル名だけが必要なので、両方に対応できるようにする必要があった
            // 変数名を「GetSavePath」から「GetSaveName」に変更し、拡張子を引数で受け取るようにして、両方の用途に対応できるようにした
            // ISOに関しては、ファイル名(hoge.iso)とラベル名は異なる扱いが必要なので、GetSaveNameはファイル名(hoge.iso)を返すようにし、
            // ISOのラベル名は別途GetVolumeLabelメソッドを作ってそこから取得するようにする
            return $"Backup_{today}.{extension}";
        }

        /*
        private string GetVolumeLabel()
        {
            // D&Dされた「今の瞬間」の日付を取得する
            string today = DateTime.Now.ToString("yyyyMMdd");
            // ISOのボリューム識別子は、A_SAMPLE_DISK や BACKUP_20260517 みたいな文字列を作って返す
            return $"Backup_{today}";
        }
        */

        private string CreateZip(string[] sourceItems, string destinationZipPath, IProgress<int> progress, CancellationToken token)
        {
            /*
            //StatusText.Text = "Zipの作成中... しばらくお待ちください。"; 
            ここに書いてはいけない。なぜなら、CreateZipはTask.Runの中で呼び出されるため、UIスレッドから呼び出されるわけではない。
            UIスレッド以外からUI要素（StatusTextなど）にアクセスしようとすると、例外が発生する。
            もしステータステキストを更新したい場合は、Dispatcher.Invokeを使ってUIスレッドで実行する必要がある。
            */

            if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);

            // 事前にすべての対象ファイルの総容量（バイト数）を計算する（進捗計算用）
            long totalBytes = 0;
            long currentBytesCopied = 0;

            foreach (string item in sourceItems)
            {
                if (Directory.Exists(item))
                {
                    foreach (string file in Directory.GetFiles(item, "*.*", SearchOption.AllDirectories))
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                }
                else if (File.Exists(item))
                {
                    totalBytes += new FileInfo(item).Length;
                }
            }

            // 万が一、空のフォルダなどで総容量が0だった場合の対策
            if (totalBytes == 0) totalBytes = 1;

            // zipファイルを作成するためのZipArchiveを開く（存在しない場合は新規作成される）
            using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

            // droppedItemsはユーザーがD&Dしたファイル（フォルダ）自体のパス
            foreach (string referenceFolder in sourceItems)
            {
                // ループの最初で一応キャンセルチェック
                token.ThrowIfCancellationRequested();

                // フォルダがD&Dされた場合
                if (Directory.Exists(referenceFolder))
                {
                    DirectoryInfo di = new DirectoryInfo(referenceFolder);
                    foreach (string targetFile in Directory.GetFiles(referenceFolder, "*.*", SearchOption.AllDirectories))
                    {
                        token.ThrowIfCancellationRequested();

                        string entryName = Path.Combine(di.Name, Path.GetRelativePath(referenceFolder, targetFile));

                        var entry = archive.CreateEntry(entryName);

                        using (var fs = new FileStream(targetFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var es = entry.Open())
                        {
                            // コピーを自前でループさせる（どんな環境でも確実に動き、確実に止まる）
                            byte[] buffer = new byte[8192]; // 8KBずつのバケツ
                            int bytesRead;

                            // 元ファイルからデータを8KB読み込める間、ずーっとループする
                            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                // 8KB書き込む直前に、毎回キャンセルボタンが押されてないかチェック
                                token.ThrowIfCancellationRequested();

                                // Zipの土管に書き込む
                                es.Write(buffer, 0, bytesRead);

                                // 書き込んだバイト数を加算し、％を計算して画面に送る
                                currentBytesCopied += bytesRead;
                                int percentComplete = (int)((currentBytesCopied * 100) / totalBytes);
                                progress?.Report(percentComplete);
                            }
                        }
                    }
                }
                // ファイルがD&Dされた場合
                else if (File.Exists(referenceFolder))
                {
                    token.ThrowIfCancellationRequested();

                    var entry = archive.CreateEntry(Path.GetFileName(referenceFolder));
                    using (var fs = new FileStream(referenceFolder, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var es = entry.Open())
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            es.Write(buffer, 0, bytesRead);

                            // ★ 書き込んだバイト数を加算し、％を計算して画面に送る
                            currentBytesCopied += bytesRead;
                            int percentComplete = (int)((currentBytesCopied * 100) / totalBytes);
                            progress?.Report(percentComplete);
                        }
                    }
                }
            }
            // Mainメソッドに「ここに作りました！」と報告する（戻り値をメインのWindow_Dropメソッドに返す）
            return destinationZipPath;
        }

        private void CreateIso(string sourceZipPath, string destinationIsoPath, CancellationToken token)
        {
            /*
            // StatusText.Text = "ISOの作成中... しばらくお待ちください。"; 
            ここに書くのはNG。なぜなら、CreateIsoはTask.Runの中で呼び出されるため、UIスレッドから呼び出されるわけではない。
            UIスレッド以外からUI要素（StatusTextなど）にアクセスしようとすると、例外が発生する。
            もしステータステキストを更新したい場合は、Dispatcher.Invokeを使ってUIスレッドで実行する必要がある。
            */

            token.ThrowIfCancellationRequested(); // ここでもキャンセルが要求されていないか確認する。キャンセルされていたら、OperationCanceledExceptionがスローされる。

            // --- ステップ2: ISOの作成 (DiscUtilsなどのライブラリを想定) ---
            var builder = new CDBuilder();
            builder.UseJoliet = true;

            // ISOのボリューム識別子を設定
            // ハードコートする場合
            //builder.VolumeIdentifier = "A_SAMPLE_DISK";
            // today変数の定義がない場合直接代入する例　例: 20240601_153045
            //string volumelabel = DateTime.Now.ToString("yyyyMMdd");
            // GetVolumeLabelメソッドを作ってそこから取得する例
            //builder.VolumeIdentifier = "GetVolumeLabel()";

            // メソッドからの取得でうまくいかないため、直接代入する方法に変更
            builder.VolumeIdentifier = $"Backup_{DateTime.Now:yyyyMMdd}";

            // 重要: builder.AddFile(ISO内でのパス, ソースファイルへのパス)
            // 引数には、ISO内でのファイルの名前を指定するパスと、実際に存在するソースファイルへのパスの両方が必要
            builder.AddFile(Path.GetFileName(sourceZipPath), sourceZipPath);
            builder.Build(destinationIsoPath);
        }
    }
}