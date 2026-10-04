using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace IconSpike;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // --selftest: サンプルを取得 → キャッシュから再取得 → ログを書いて終了（自動確認用）
                SelfTest = desktop.Args?.Contains("--selftest") == true,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
