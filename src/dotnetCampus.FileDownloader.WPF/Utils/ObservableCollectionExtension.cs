using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace dotnetCampus.FileDownloader.WPF.Utils
{
    public static class ObservableCollectionExtension
    {
        public static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> list)
        {
            foreach (var temp in list)
            {
                observableCollection.Add(temp);
            }
        }
    }
}
