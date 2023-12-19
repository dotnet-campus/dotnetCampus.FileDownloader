using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoFileDownloader.Presentation
{
    public partial record AboutModel(INavigator Navigator)
    {
        public void CloseAbout()
        {
            _ = Navigator.NavigateBackAsync(this);
        }
    }
}
