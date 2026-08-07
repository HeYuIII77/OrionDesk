using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading;

namespace OrionDesk.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // 单例模式检查
        const string mutexName = "OrionDesk_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // 已经有一个实例在运行
            System.Windows.MessageBox.Show("OrionDesk 已经在运行中。\n请检查系统托盘图标。",
                "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        // 全局异常处理 — 防止未捕获异常导致崩溃
        DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"[全局] UI 线程未处理异常: {args.Exception}");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Debug.WriteLine($"[全局] AppDomain 异常: {args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Debug.WriteLine($"[全局] 未观察的 Task 异常: {args.Exception}");
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
