using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using dotnetCampus.FileDownloader.WPF.Model;
using Microsoft.Extensions.Logging;
using Path = System.IO.Path;

namespace dotnetCampus.FileDownloader.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ViewModel.Init();
        }

        public MainViewModel ViewModel { get; } = new MainViewModel();

        private void AddFileDownload_OnClick(object sender, RoutedEventArgs e)
        {
            ShowDownloadDialog();

            var text = Clipboard.GetText();

            if (Regex.IsMatch(text, @"^((https|http|ftp|rtsp|mms)?:\/\/)[^\s]+"))
            {
                // 先清除，然后赋值，这样可以自动获取文件名
                ViewModel.AddFileDownloadViewModel.CurrentDownloadFilePath = string.Empty;
                ViewModel.AddFileDownloadViewModel.CurrentDownloadUrl = text;
            }
        }

        private void ShowDownloadDialog()
        {
            MainGrid.IsEnabled = false;
            DownloadContentDialog.Visibility = Visibility.Visible;
        }

        private void HideDownloadDialog()
        {
            MainGrid.IsEnabled = true;
            DownloadContentDialog.Visibility = Visibility.Hidden;

            ViewModel.AddFileDownloadViewModel.CurrentDownloadUrl = string.Empty;
            ViewModel.AddFileDownloadViewModel.CurrentDownloadFilePath = string.Empty;
        }

        private void AddDownloadFilePage_OnDownloadClick(object sender, EventArgs e)
        {
            ViewModel.AddDownloadFile();

            HideDownloadDialog();

            DependencyObject border = VisualTreeHelper.GetChild(DownloadItemList, 0);
            ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            scrollViewer.ScrollToBottom();
        }

        private void CleanDownloadItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (DownloadItemList.SelectedItems.Count == 0)
            {
                ViewModel.DownloadFileInfoList.Clear();
            }
            else
            {
                foreach (var downloadFileInfo in DownloadItemList.SelectedItems.OfType<DownloadFileInfo>().ToList())
                {
                    ViewModel.DownloadFileInfoList.Remove(downloadFileInfo);
                }
            }
        }

        private void DownloadContentDialog_OnClosed(object? sender, EventArgs e)
        {
            HideDownloadDialog();
        }

        public static readonly RoutedUICommand OpenFileCommand = new RoutedUICommand();
        public static readonly RoutedUICommand OpenFolderCommand = new RoutedUICommand();
        public static readonly RoutedUICommand RemoveItemCommand = new RoutedUICommand();

        private void OpenFileCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = downloadFileInfo.IsFinished;
        }

        private void OpenFileCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                return;
            }

            var processStartInfo = new ProcessStartInfo("explorer")
            {
                ArgumentList =
                {
                    downloadFileInfo.FilePath
                }
            };

            Process.Start(processStartInfo);
        }

        private void OpenFolderCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = downloadFileInfo.IsFinished;
        }

        private void OpenFolderCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{downloadFileInfo.FilePath}\"");
        }

        private void RemoveItemCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = true;
        }

        private void RemoveItemCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Parameter is DownloadFileInfo downloadFileInfo))
            {
                return;
            }

            ViewModel.DownloadFileInfoList.Remove(downloadFileInfo);
        }
    } 
}
