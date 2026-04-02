using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace bug_WinUI3_json_from_weather_sample
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnTryJsonStart(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = SerializeExtra.Demonstrate_Bug_Program.Demonstrate_Bug_Main();
                uiResult.Text = $"JSON Start Output:\n{json}";

            }
            catch (Exception ex)
            {
                uiResult.Text = $"EXCEPTION: JSON Start:\n{ex.Message}";
            }
        }
        private void OnTryJsonFix1(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = SerializeExtra_Fix1.Demonstrate_Bug_Program.Demonstrate_Bug_Main();
                uiResult.Text = $"JSON Fix #1 Output:\n{json}";

            }
            catch (Exception ex)
            {
                uiResult.Text = $"EXCEPTION: JSON Fix #1:\n{ex.Message}";
            }
        }
    }
}
