using System;
using System.Collections.Generic;
using System.Text;
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
    /// AddDownloadFilePage.xaml 的交互逻辑
    /// </summary>
    public partial class AddDownloadFilePage : UserControl
    {
        public AddDownloadFilePage()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
            "FilePath", typeof(string), typeof(AddDownloadFilePage), new PropertyMetadata(default(string)));

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(
            "Url", typeof(string), typeof(AddDownloadFilePage), new PropertyMetadata(default(string),
                (o, args) =>
                {
                    var addDownloadFilePage = (AddDownloadFilePage)o;
                    var url = addDownloadFilePage.Url;

                    if (string.IsNullOrEmpty(addDownloadFilePage.FilePath))
                    {
                        addDownloadFilePage.FilePath = GetFileName(url);
                    }
                }));

        private static string GetFileName(string url)
        {
            Uri uri = new Uri(url);

            var fileName = System.IO.Path.GetFileName(uri.AbsolutePath);
            return Uri.UnescapeDataString(fileName);
        }

        public string Url
        {
            get { return (string)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        public event EventHandler DownloadClick;

        private void DownloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            DownloadClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
