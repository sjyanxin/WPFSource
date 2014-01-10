//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//--------------------------------------------------------------------------- 

using System; 
using System.Globalization; 
using System.Windows;
using System.Windows.Data; 

namespace System.Windows.Controls
{
    /// <summary> 
    ///     Converts Boolean to SelectiveScrollin----entation based on the given parameter.
    /// </summary> 
    [Localizability(LocalizationCategory.NeverLocalize)] 
    internal sealed class BooleanToSelectiveScrollin----entationConverter : IValueConverter
    { 
        /// <summary>
        ///     Convert Boolean to SelectiveScrollin----entation
        /// </summary>
        /// <param name="value">Boolean</param> 
        /// <param name="targetType">SelectiveScrollin----entation</param>
        /// <param name="parameter">SelectiveScrollin----entation that should be used when the Boolean is true</param> 
        /// <param name="culture">null</param> 
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { 
            if (value is bool && parameter is SelectiveScrollin----entation)
            {
                var valueAsBool = (bool)value;
                var parameterSelectiveScrollin----entation = (SelectiveScrollin----entation)parameter; 

                if (valueAsBool) 
                { 
                    return parameterSelectiveScrollin----entation;
                } 
            }

            return SelectiveScrollin----entation.Both;
        } 

        /// <summary> 
        ///     Not implemented 
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        {
            throw new NotImplementedException();
        }
    } 
}

