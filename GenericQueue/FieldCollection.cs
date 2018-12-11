using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Serialization;

namespace GenericQueue
{
    [XmlRootAttribute("Details")]
    public class FieldCollection
    {
        [XmlElement("Field")]
        public Field[] Fields { get; set; }
    }

    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            SolidColorBrush solidColorBrush = new SolidColorBrush();
            try
            {
                solidColorBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#" + value.ToString()));
                return solidColorBrush;
            }
            catch(Exception ex)
            {
                return solidColorBrush;
            }
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
            //if (value is bool)
            //{
            //    if ((bool)value == true)
            //        return "yes";
            //    else
            //        return "no";
            //}
            //return "no";
        }
    }

    public class EqualityConverter : IValueConverter
    {
        MainWindow MW;
        public EqualityConverter(MainWindow mw)
        {
            MW = mw;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var t = MW.SelectedIndex;
            SolidColorBrush solidColorBrush = new SolidColorBrush();
            try
            {
                solidColorBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#" + value.ToString()));
                return solidColorBrush;
            }
            catch (Exception ex)
            {
                return solidColorBrush;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
            //if (value is bool)
            //{
            //    if ((bool)value == true)
            //        return "yes";
            //    else
            //        return "no";
            //}
            //return "no";
        }
    }
}
