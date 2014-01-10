using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace System.Windows.Data
{
    public interface IMultiValueConverter
    {
        // Methods
        object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
        object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
    }

 
}
