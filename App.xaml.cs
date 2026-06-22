using System;
using System.Windows;

namespace ProcessMonitor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 添加全局异常处理
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            string errorMessage = $"未处理的异常: {ex.Message}\n堆栈跟踪: {ex.StackTrace}";
            System.IO.File.AppendAllText("error.log", $"{DateTime.Now}: {errorMessage}\n");
            MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string errorMessage = $"Dispatcher 异常: {e.Exception.Message}\n堆栈跟踪: {e.Exception.StackTrace}";
        System.IO.File.AppendAllText("error.log", $"{DateTime.Now}: {errorMessage}\n");
        MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // 防止应用程序关闭
    }
}

