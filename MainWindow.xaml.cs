using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using System.IO;
using System.Windows;
using Windows.UI.Notifications;

namespace MySimpleISOBuilder // MainWindow.xaml.Core.csと同じ名前空間にする
{
    public partial class MainWindow : Window　// partialをつけることで、MainWindow.xaml.csと共通化
    {
        // 変数定義
        private CancellationTokenSource? _cts;　// CancellationTokenのクラスレベルでの定義をすることで、他のメソッド（例: CancelButton）からもアクセスできるようにする
        private bool _isProcessing = false; // 処理中かどうかを示すフラグ

        public struct MyProgressData // 数値と文字列をセットにして画面に届けるための箱
        {
            public int Percent { get; set; }   // プログレスバーの％ (0〜100)
            public string Message { get; set; } // StatusTextに表示したい文字
        }

        // 通知メソッド。Microsoft.Toolkit.Uwp.Notificationsが必要。
        private void ShowToastNotification(string title, string message)
        {
            var toastContent = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .GetToastContent();

            var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
            xmlDoc.LoadXml(toastContent.GetContent());

            var toast = new ToastNotification(xmlDoc)
            {
                Tag = "MySimpleISOBuilderNotification"
            };

            ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }

        public MainWindow()
        {
            InitializeComponent();

            /*
             * jsonで設定を管理する場合のコード例。Microsoft.Extensions.Configuration.Jsonが必要。
             * 
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var outputPath = config["AppSettings:DefaultOutputPath"];
            }
            catch (Exception ex)
            {
                // appsettings.jsonの読み込みに失敗した場合は、デスクトップに出力するようにする
                // 例外の内容をユーザーに知らせる
                MessageBox.Show(this, "appsettings.jsonの読み込みに失敗しました。デスクトップに出力します。\n\nエラー内容:\n" + ex.Message);
            }
            */
        }

        // 何かしらのものがドラッグされたときに呼ばれるメソッド
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // 処理中の場合とファイル以外がD&Dされた場合に弾く
            if (_isProcessing)
            {
                e.Effects = DragDropEffects.None; // 処理中の場合は、ドロップ不可
                e.Handled = true;
            }
            else if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None; // ファイル・フォルダではない場合は、ドロップ不可
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.Copy; // ドロップ可能な場合は、コピーのカーソルを表示
            }
        }

        // ファイルがドロップされた時に呼ばれるメソッド。非同期処理に対応させるために、"private void" から "async"を追加
        private async void Window_Drop(object sender, DragEventArgs e)
        {
            // 万が一にD&D出来てしまった場合を想定したフェイルセーフ
            if (_isProcessing)
            {
                MessageBox.Show(this, "現在、処理中です。追加D&Dはできません。");
                return; // 既に処理中なら何もしない
            }
            // そもそも内容が空かどうかを確かめる。なければ終了
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            /*
            // パス取得（Core.csのGetSavePathメソッドを使う前のコード）
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string zipPath = Path.Combine(desktopPath, $"Backup_{today}.zip");
            string isoPath = Path.Combine(desktopPath, $"Backup_{today}.iso");
            */

            string[] droppedItems = (string[])e.Data.GetData(DataFormats.FileDrop);
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // zipPathとisoPathの命名ルールをCore.csのGetSaveNameメソッドに統一する。これで、同じ命名ルールでzipとisoの両方が保存されるようになる。
            string zipPath = Path.Combine(desktopPath, GetSaveName("zip"));
            string isoPath = Path.Combine(desktopPath, GetSaveName("iso"));

            // progressが叩かれた際に成形される関数的なもの。
            // MyProgressData型のdataを受け取る。MyProgressDataは、intのPercentとstringのMessageをセットにした構造体。
            IProgress<MyProgressData> progress = new Progress<MyProgressData>(data =>
            {
                MyProgressBar.Value = data.Percent;
                ProgressText.Text = $"{data.Percent}% 完了";
                StatusText.Text = data.Message;
            });

            try
            {
                // 実行前に初期化してトークンを発行する
                _cts = new CancellationTokenSource();
                CancellationToken token = _cts.Token;

                // 処理中フラグを立てる
                _isProcessing = true;

                // 処理開始を知らせる
                StatusText.Text = "処理を開始しています...";
                progress.Report(new MyProgressData { Percent = 0, Message = "処理を開始しています..." });

                // メインの処理（ZIPの作成とISOの作成)
                // 別スレッドで重い処理を行うために、Task.Runを使って非同期に処理を実行する
                await Task.Run(() =>
                {
                    // キャンセルチェック
                    token.ThrowIfCancellationRequested();

                    // ZIP作成までで99%を使い切る想定で、ZIPの進捗は0～99%の範囲で報告するためのProgressオブジェクトを作成。
                    // CreateZipの中で、zipProgress.Report(50);のように報告すると、ここで定義したラムダ式(v => progress.Report(...))が呼ばれ、0.99倍される。
                    // この時点では何も報告されていない。
                    // Progress<int>が設計図で、zipProgressや全部小文字のprogressはそれをもとに生成された実体（＝インスタンス）のあだ名みたいなもの。
                    var zipProgress = new Progress<int>(v => progress.Report(new MyProgressData
                    {
                        // v は 0 ～ 100 で戻ってくるので、0.99 を掛けて最大 99% にする
                        Percent = (int)(v * 0.99),
                        Message = "[1/2] ZIPアーカイブを作成中..."
                    })
                    );

                    // ZIPの作成（引数に zipProgress を渡さないと報告が動作しない）
                    // これはCreateZipに戻り値の返却を求めない場合（メソッド定義時にvoidと記述）した場合の呼び出しかた
                    // CreateZip(droppedItems, zipPath, token);
                    // これはCreateZipに戻り値の返却を求める場合（stringと記述）した場合の呼び出しかた。
                    // CreateZipから返されたZipファイルのパス(return hoge;の中身）を受け取ってtempPathに代入。
                    string tempPath = CreateZip(droppedItems, zipPath, zipProgress, token);

                    // キャンセルチェック
                    token.ThrowIfCancellationRequested();

                    // ISO作成開始段階での進捗更新
                    // ISOの作成はZIPの作成が終わった後に行うため、ここで99%を報告しておく。ISOの作成中は99%～100%の範囲で報告する想定。
                    progress.Report(new MyProgressData { Percent = 99, Message = "[2/2] ISOイメージにビルド中（これには数分かかる場合があります）..." });

                    // ISOの作成実行
                    CreateIso(tempPath, isoPath, token);

                    // すべて完了時の進捗更新
                    progress.Report(new MyProgressData { Percent = 100, Message = "すべての処理が完了しました！" });
                }, token);

                // 裏でウインドウが出て気づかなかったので、一時的にウインドウを前面に出す（ウインドウ更新が見逃されないようにするため）
                this.Activate();
                this.Topmost = true;
                this.Topmost = false;

                // 処理完了のメッセージを表示
                MessageBox.Show(this, "ISOの作成が完了しました！");
                StatusText.Text = "完了! 次にISO化するファイル・フォルダを待機中...";
                ProgressText.Text = "(完了済み)";

                // 通知飛ばしてみる
                ShowToastNotification("ISOビルド完了", $"ISO作成完了:{isoPath}");

                // 処理成功後の後片付け(ctsトークンの破棄)
                _cts?.Dispose();
                _cts = null;
            }
            // キャンセルされたときの例外をキャッチする
            catch (OperationCanceledException)
            {
                // キャンセルボタンが押されたらすぐに100%までもっていき、中途半端なところで止まらないようにする（これがないとキャンセル時点のところで止まったままになる）
                progress.Report(new MyProgressData { Percent = 100, Message = "キャンセルされました" });

                // もし中途半端なZipやIsoが残っていたら、ここで綺麗に削除する
                if (File.Exists(zipPath)) try { File.Delete(zipPath); } catch { }
                if (File.Exists(isoPath)) try { File.Delete(isoPath); } catch { }

                // 画面表示の更新
                StatusText.Text = "処理がキャンセルされました。次にISO化するファイル・フォルダを待機中...";
                ProgressText.Text = "(キャンセル済み)";

                // 処理失敗後の後片付け(ctsトークンの破棄)
                _cts?.Dispose();
                _cts = null;

                // キャンセルボタンが押されたということはメインウインドウにフォーカスがあるはずなので、正常完了時と異なり、わざわざActivateする必要はないと思われる。
                MessageBox.Show(this, "キャンセルボタンが押されたため、処理がキャンセルされました。");
            }
            // その他の例外をキャッチする
            catch (Exception ex)
            {
                // 画面表示の更新
                MessageBox.Show(this, $"エラーが発生しました:\n{ex.Message}");
                StatusText.Text = "注意：直近でエラー発生";
                ProgressText.Text = "(エラー発生)";

                // もし中途半端なZipやIsoが残っていたら、ここで綺麗に削除する
                if (File.Exists(zipPath)) try { File.Delete(zipPath); } catch { }
                if (File.Exists(isoPath)) try { File.Delete(isoPath); } catch { }

                // 処理失敗後の後片付け(ctsトークンの破棄)
                _cts?.Dispose();
                _cts = null;
            }
            finally
            {
                // 処理終了後にフラグを下げる
                _isProcessing = false;
            }
        }

        // キャンセルボタンがクリックされたときに呼ばれるメソッド
        // privateかpublicか？→publicにする必要がある。なぜなら、XAMLで定義されたボタンのClickイベントから呼び出されるため、publicでないとアクセスできない。
        public void CancelButton(object sender, RoutedEventArgs e)
        {
            // _cts が null でないときだけ Cancel() を呼ぶ安全な書き方
            if (_cts != null)
            {
                // Cancelメソッドだけ呼ぶ。フィードバックは、Window_Dropのcatch (OperationCanceledException)の中で行う。
                _cts.Cancel();
                //MessageBox.Show(this, "キャンセルボタンが押されました。");
            }
            else
            {
                MessageBox.Show(this, "実行されていません。またはすでにキャンセル・終了しています。");
            }
        }

        // いちいち通知センターを開いて消去するのが面倒なので、アプリ上から本アプリ由来の通知をワンクリックで削除できるようにボタンを設置。
        public void RemoveNotiButton(object sender, RoutedEventArgs e)
        {
            // 本プログラムに関係する通知だけを消去する
            ToastNotificationManagerCompat.History.Remove("MySimpleISOBuilderNotification");
        }

    }
}