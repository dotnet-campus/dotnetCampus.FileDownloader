using System;
using System.Windows.Input;

namespace dotnetCampus.FileDownloader.WPF
{
    public class DelegateCommand : ICommand
    {
        public Func<object, bool>? CanExecuteDelegate { set; get; }

        public Action<object>? ExecuteDelegate { set; get; }

        public bool CanExecute(object parameter)
        {
            return CanExecuteDelegate?.Invoke(parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            ExecuteDelegate?.Invoke(parameter);
        }

        public event EventHandler? CanExecuteChanged;
    }
}