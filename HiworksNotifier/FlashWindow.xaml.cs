using System;
using System.Windows;

namespace HiworksNotifier
{
    public partial class FlashWindow : Window
    {
        public FlashWindow()
        {
            InitializeComponent();
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
