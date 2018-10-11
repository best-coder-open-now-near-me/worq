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

namespace GenericQueue
{

    public class ExButton : Button
    {
        //Unless you override the style it will never be rendered
        static ExButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ExButton), new FrameworkPropertyMetadata(typeof(ExButton)));
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        "Text",
        typeof(string),
        typeof(ExButton),
        new UIPropertyMetadata(string.Empty));

        public static readonly DependencyProperty IDProperty = DependencyProperty.Register(
        "ID",
        typeof(int),
        typeof(ExButton));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public int ID
        {
            get { return (int)GetValue(IDProperty); }
            set { SetValue(IDProperty, value); }
        }
    }

}
