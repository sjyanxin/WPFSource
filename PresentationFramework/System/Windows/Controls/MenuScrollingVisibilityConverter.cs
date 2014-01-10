//---------------------------------------------------------------------------- 
//
// <copyright file="MenuScrollingVisibilityConverter.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Used to show or hide arrow buttons in scrolling menus. 
// 
//---------------------------------------------------------------------------
 
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Provider; 
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using System.Windows.Data; 
using System.Windows.Media;
using MS.Internal; 

namespace System.Windows.Controls
{
    /// <summary> 
    ///     Data binding converter to handle the visibility of repeat buttons in scrolling menus.
    /// </summary> 
    public sealed class MenuScrollingVisibilityConverter : IMultiValueConverter 
    {
        /// <summary> 
        /// Convert a value.  Called when moving a value from source to target.
        /// </summary>
        /// <param name="values">values as produced by source binding</param>
        /// <param name="targetType">target type</param> 
        /// <param name="parameter">converter parameter</param>
        /// <param name="culture">culture information</param> 
        /// <returns> 
        ///     Converted value.
        /// 
        ///     System.Windows.DependencyProperty.UnsetValue may be returned to indicate that
        ///     the converter produced no value and that the fallback (if available)
        ///     or default value should be used instead.
        /// 
        ///     Binding.DoNothing may be returned to indicate that the binding
        ///     should not transfer the value or use the fallback or default value. 
        /// </returns> 
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        { 
            //
            // Parameter Validation
            //
 
            Type doubleType = typeof(double);
            if (parameter == null || 
                values == null || 
                values.Length != 4 ||
                values[0] == null || 
                values[1] == null ||
                values[2] == null ||
                values[3] == null ||
                !typeof(Visibility).IsAssignableFrom(values[0].GetType()) || 
                !doubleType.IsAssignableFrom(values[1].GetType()) ||
                !doubleType.IsAssignableFrom(values[2].GetType()) || 
                !doubleType.IsAssignableFrom(values[3].GetType()) ) 
            {
                return DependencyProperty.UnsetValue; 
            }

            Type paramType = parameter.GetType();
            if (!(doubleType.IsAssignableFrom(paramType) || typeof(string).IsAssignableFrom(paramType))) 
            {
                return DependencyProperty.UnsetValue; 
            } 

            // 
            // Conversion
            //

            // If the scroll bar should be visible, then so should our buttons 
            Visibility computedVerticalScrollBarVisibility = (Visibility)values[0];
            if (computedVerticalScrollBarVisibility == Visibility.Visible) 
            { 
                double target;
 
                if (parameter is string)
                {
                    target = Double.Parse(((string)parameter), NumberFormatInfo.InvariantInfo);
                } 
                else
                { 
                    target = (double)parameter; 
                }
 
                double verticalOffset = (double)values[1];
                double extentHeight = (double)values[2];
                double viewportHeight = (double)values[3];
 
                if (extentHeight != viewportHeight) // Avoid divide by 0
                { 
                    // Calculate the percent so that we can see if we are near the edge of the range 
                    double percent = Math.Min(100.0, Math.Max(0.0, (verticalOffset * 100.0 / (extentHeight - viewportHeight))));
 
                    if (DoubleUtil.AreClose(percent, target))
                    {
                        // We are at the end of the range, so no need for this button to be shown
                        return Visibility.Collapsed; 
                    }
                } 
 
                return Visibility.Visible;
            } 

            return Visibility.Collapsed;
        }
 
        /// <summary>
        /// Not Supported 
        /// </summary> 
        /// <param name="value">value, as produced by target</param>
        /// <param name="targetTypes">target types</param> 
        /// <param name="parameter">converter parameter</param>
        /// <param name="culture">culture information</param>
        /// <returns>Nothing</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) 
        {
            return new object[] { Binding.DoNothing }; 
        } 
    }
} 

