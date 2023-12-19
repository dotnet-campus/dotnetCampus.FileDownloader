namespace UnoFileDownloader.Presentation
{
    public class ShellModel
    {
        private readonly INavigator _navigator;

        public ShellModel(
            INavigator navigator)
        {
            _navigator = navigator;
            //_ = Start(); // 将会导致莫名跳转到 MainPage 界面
        }

        public async Task Start()
        {
            await _navigator.NavigateViewModelAsync<MainModel>(this);
        }
    }
}
