//---------------------------------------------------------------------------- 
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Description: FontStretch type converter. 
//
// History: 
//  01/25/2005 mleonov - Created. 
//
//--------------------------------------------------------------------------- 

using System;
using System.IO;
using System.ComponentModel; 
using System.ComponentModel.Design.Serialization;
using System.Reflection; 
using MS.Internal; 
using System.Windows.Media;
using System.Text; 
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security; 

using SR=MS.Internal.PresentationCore.SR; 
using SRID=MS.Internal.PresentationCore.SRID; 

namespace System.Windows 
{
    /// <summary>
    /// FontStretchConverter class parses a font stretch string.
    /// </summary> 
    public sealed class FontStretchConverter : TypeConverter
    { 
        /// <summary> 
        /// CanConvertFrom
        /// </summary> 
        public override bool CanConvertFrom(ITypeDescriptorContext td, Type t)
        {
            if (t == typeof(string))
            { 
                return true;
            } 
            else 
            {
                return false; 
            }
        }

        /// <summary> 
        /// TypeConverter method override.
        /// </summary> 
        /// <param name="context">ITypeDescriptorContext</param> 
        /// <param name="destinationType">Type to convert to</param>
        /// <returns>true if conversion is possible</returns> 
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(InstanceDescriptor) || destinationType == typeof(string))
            { 
                return true;
            } 
 
            return base.CanConvertTo(context, destinationType);
        } 

        /// <summary>
        /// ConvertFrom - attempt to convert to a FontStretch from the given object
        /// </summary> 
        /// <exception cref="NotSupportedException">
        /// A NotSupportedException is thrown if the example object is null or is not a valid type 
        /// which can be converted to a FontStretch. 
        /// </exception>
        public override object ConvertFrom(ITypeDescriptorContext td, CultureInfo ci, object value) 
        {
            if (null == value)
            {
                throw GetConvertFromException(value); 
            }
 
            String s = value as string; 

            if (null == s) 
            {
                throw new ArgumentException(SR.Get(SRID.General_BadType, "ConvertFrom"), "value");
            }
 
            FontStretch fontStretch = new FontStretch();
            if (!FontStretches.FontStretchStringToKnownStretch(s, ci, ref fontStretch)) 
                throw new FormatException(SR.Get(SRID.Parsers_IllegalToken)); 

            return fontStretch; 
        }

        /// <summary>
        /// TypeConverter method implementation. 
        /// </summary>
        /// <exception cref="NotSupportedException"> 
        /// An NotSupportedException is thrown if the example object is null or is not a FontStretch, 
        /// or if the destinationType isn't one of the valid destination types.
        /// </exception> 
        /// <param name="context">ITypeDescriptorContext</param>
        /// <param name="culture">current culture (see CLR specs)</param>
        /// <param name="value">value to convert from</param>
        /// <param name="destinationType">Type to convert to</param> 
        /// <returns>converted value</returns>
        ///<SecurityNote> 
        ///     Critical: calls InstanceDescriptor ctor which LinkDemands 
        ///     PublicOK: can only make an InstanceDescriptor for FontStretch.FromOpenTypeStretch, not an arbitrary class/method
        ///</SecurityNote> 
        [SecurityCritical]
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType != null && value is FontStretch) 
            {
                if (destinationType == typeof(InstanceDescriptor)) 
                { 
                    MethodInfo mi = typeof(FontStretch).GetMethod("FromOpenTypeStretch", new Type[]{typeof(int)});
                    FontStretch c = (FontStretch)value; 
                    return new InstanceDescriptor(mi, new object[]{c.ToOpenTypeStretch()});
                }
                else if (destinationType == typeof(string))
                { 
                    FontStretch c = (FontStretch)value;
                    return ((IFormattable)c).ToString(null, culture); 
                } 
            }
 
            // Pass unhandled cases to base class (which will throw exceptions for null value or destinationType.)
            return base.ConvertTo(context, culture, value, destinationType);
        }
    } 
}

