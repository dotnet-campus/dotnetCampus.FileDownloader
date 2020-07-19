using System;
using System.Collections.Generic;
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

            DataContext = ViewModel;

            ViewModel.Init();
        }

        private void AddFileDownload_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void AddFileDownload_OnClick(object sender, RoutedEventArgs e)
        {
            
        }

        private void AddDownloadFilePage_OnDownloadClick(object? sender, EventArgs e)
        {
            
        }

        private MainViewModel ViewModel { get; }  = new MainViewModel();

    }
}
