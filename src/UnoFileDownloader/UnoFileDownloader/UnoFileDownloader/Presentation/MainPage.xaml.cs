using System.Diagnostics;

namespace UnoFileDownloader.Presentation
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            DataContextChanged += MainPage_DataContextChanged;
        }

        public BindableMainModel ViewModel => (BindableMainModel) DataContext;

        private void MainPage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args.NewValue is BindableMainModel model)
            {
                model.DownloadFileInfoViewList.CollectionChanged += DownloadFileInfoViewList_CollectionChanged;
                UpdateTaskListNoItemsTextBlock();
            }
            else
            {
#if DEBUG
                // 谁，是谁，乱改 DataContext 的类型！
                Debugger.Break();
#endif
            }
        }

        private void DownloadFileInfoViewList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTaskListNoItemsTextBlock();
        }

        /// <summary>
        /// 更新任务列表的“没有任务”文本块的可见性。
        /// </summary>
        private void UpdateTaskListNoItemsTextBlock()
        {
            TaskListNoItemsTextBlock.Visibility =
                ViewModel.DownloadFileInfoViewList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
