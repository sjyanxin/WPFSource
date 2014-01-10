//---------------------------------------------------------------------------- 
//
// <copyright file="VectorCollectionValueSerializer.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This file was generated, please do not edit it directly. 
// 
// Please see http://wiki/default.aspx/Microsoft.Projects.Avalon/MilCodeGen.html for more information.
// 
//---------------------------------------------------------------------------

using MS.Internal;
using MS.Internal.KnownBoxes; 
using MS.Internal.Collections;
using MS.Internal.PresentationCore; 
using MS.Utility; 
using System;
using System.Collections; 
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization; 
using System.Reflection;
using System.Runtime.InteropServices; 
using System.ComponentModel.Design.Serialization; 
using System.Text;
using System.Windows; 
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation; 
using System.Windows.Media.Composition;
using System.Windows.Media.Imaging; 
using System.Windows.Markup; 
using System.Windows.Media.Converters;
using System.Security; 
using System.Security.Permissions;
using SR=MS.Internal.PresentationCore.SR;
using SRID=MS.Internal.PresentationCore.SRID;
#pragma warning disable 1634, 1691  // suppressing PreSharp warnings 

namespace System.Windows.Media.Converters 
{ 
    /// <summary>
    /// VectorCollectionValueSerializer - ValueSerializer class for converting instances of strings to and from VectorCollection instances 
    /// This is used by the MarkupWriter class.
    /// </summary>
    public class VectorCollectionValueSerializer : ValueSerializer
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
            if (!(value is VectorCollection))
            {
                return false; 
            }
 
            return true; 

        } 

        /// <summary>
        /// Converts a string into a VectorCollection.
        /// </summary> 
        public override object ConvertFromString(string value, IValueSerializerContext context)
        { 
            if (value != null) 
            {
                return VectorCollection.Parse(value ); 
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
            if (value is VectorCollection)
            { 
                VectorCollection instance = (VectorCollection) value; 

 
                #pragma warning suppress 6506 // instance is obviously not null
                return instance.ConvertToString(null, System.Windows.Markup.TypeConverterHelper.InvariantEnglishUS);
            }
 
            return base.ConvertToString(value, context);
        } 
    } 

 


}

