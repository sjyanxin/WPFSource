//---------------------------------------------------------------------------- 
//
// <copyright file="Size3DValueSerializer.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This file was generated, please do not edit it directly. 
// 
// Please see http://wiki/default.aspx/Microsoft.Projects.Avalon/MilCodeGen.html for more information.
// 
//---------------------------------------------------------------------------

using MS.Internal;
using MS.Internal.Collections; 
using MS.Internal.PresentationCore;
using MS.Utility; 
using System; 
using System.Collections;
using System.Collections.Generic; 
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Globalization; 
using System.Reflection;
using System.Runtime.InteropServices; 
using System.Text; 
using System.Windows.Markup;
using System.Windows.Media.Media3D.Converters; 
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Composition;
using System.Security; 
using System.Security.Permissions;
using SR=MS.Internal.PresentationCore.SR; 
using SRID=MS.Internal.PresentationCore.SRID; 
using System.Windows.Media.Imaging;
#pragma warning disable 1634, 1691  // suppressing PreSharp warnings 

namespace System.Windows.Media.Media3D.Converters
{
    /// <summary> 
    /// Size3DValueSerializer - ValueSerializer class for converting instances of strings to and from Size3D instances
    /// This is used by the MarkupWriter class. 
    /// </summary> 
    public class Size3DValueSerializer : ValueSerializer
    { 
        /// <summary>
        /// Returns true.
        /// </summary>
        public override bool CanConvertFromString(string value, IValueSerializerContext context) 
        {
            return true; 
        } 

        /// <summary> 
        /// Returns true if the given value can be converted into a string
        /// </summary>
        public override bool CanConvertToString(object value, IValueSerializerContext context)
        { 

            // Validate the input type 
            if (!(value is Size3D)) 
            {
                return false; 
            }

            return true;
 
        }
 
        /// <summary> 
        /// Converts a string into a Size3D.
        /// </summary> 
        public override object ConvertFromString(string value, IValueSerializerContext context)
        {
            if (value != null)
            { 
                return Size3D.Parse(value );
            } 
            else 
            {
                return base.ConvertFromString( value, context ); 
            }

        }
 
        /// <summary>
        /// Converts the value into a string. 
        /// </summary> 
        public override string ConvertToString(object value, IValueSerializerContext context)
        { 
            if (value is Size3D)
            {
                Size3D instance = (Size3D) value;
 

                #pragma warning suppress 6506 // instance is obviously not null 
                return instance.ConvertToString(null, System.Windows.Markup.TypeConverterHelper.InvariantEnglishUS); 
            }
 
            return base.ConvertToString(value, context);
        }
    }
 

 
 
}

