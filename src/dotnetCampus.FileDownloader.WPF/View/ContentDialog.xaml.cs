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
    /// ContentDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ContentDialog : UserControl
    {
        public ContentDialog()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title", typeof(string), typeof(ContentDialog), new PropertyMetadata(default(string)));

        public string Title
        {
            get { return (string) GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty ContentElementProperty = DependencyProperty.Register(
            "ContentElement", typeof(UIElement), typeof(ContentDialog), new PropertyMetadata(default(UIElement)));

        public UIElement ContentElement
        {
            get { return (UIElement) GetValue(ContentElementProperty); }
            set { SetValue(ContentElementProperty, value); }
        }

        public event EventHandler Closed = delegate { };

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Closed(this, EventArgs.Empty);
        }
    }
}
