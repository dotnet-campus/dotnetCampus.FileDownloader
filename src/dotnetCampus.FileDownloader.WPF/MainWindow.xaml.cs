using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using Microsoft.Extensions.Logging;

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

        private void AddFileDownload_OnClick(object sender, RoutedEventArgs e)
        {
            ShowDownloadDialog();
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
        }

        private void AddDownloadFilePage_OnDownloadClick(object sender, EventArgs e)
        {
            var addDownloadFilePage = (AddDownloadFilePage)sender;
            var url = addDownloadFilePage.Url;
            var file = addDownloadFilePage.FilePath;

            ViewModel.AddDownloadFile(url, file);

            HideDownloadDialog();
        }

        public MainViewModel ViewModel { get; } = new MainViewModel();

        private void DownloadContentDialog_OnClosed(object? sender, EventArgs e)
        {
            HideDownloadDialog();
        }
    }


}
