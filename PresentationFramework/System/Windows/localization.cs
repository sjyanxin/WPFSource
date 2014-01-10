//---------------------------------------------------------------------------- 
//
// <copyright file=Localization.cs company=Microsoft>
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// 
// Description: Localization.Comments & Localization.Attributes attached properties 
//
// History: 
//  12/4/2004: Garyyang Created the file
//  3/11/2005: garyyang rename Loc to Localization class
//
//--------------------------------------------------------------------------- 
using System.Collections;
using System.Diagnostics; 
using MS.Internal.Globalization; 

namespace System.Windows 
{
    //
    // Note: the class name and property name must be kept in [....]'ed with
    // Framework\MS\Internal\Globalization\LocalizationComments.cs file. 
    // Compiler checks for them by literal string comparisons.
    // 
    /// <summary> 
    /// Class defines attached properties for Comments and Localizability
    /// </summary> 
    public static class Localization
    {
        /// <summary>
        /// DependencyProperty for Comments property. 
        /// </summary>
        public static readonly DependencyProperty CommentsProperty = 
            DependencyProperty.RegisterAttached( 
                "Comments",
                typeof(string), 
                typeof(Localization)
                );

        /// <summary> 
        /// DependencyProperty for Localizability property.
        /// </summary> 
        public static readonly DependencyProperty AttributesProperty = 
            DependencyProperty.RegisterAttached(
                "Attributes", 
                typeof(string),
                typeof(Localization)
                );
 
        /// <summary>
        /// Reads the attached property Comments from given element. 
        /// </summary> 
        /// <param name="element">The element from which to read the attached property.</param>
        /// <returns>The property's value.</returns> 
        [AttachedPropertyBrowsableForType(typeof(object))]
        public static string GetComments(object element)
        {
            if (element == null) 
            {
                throw new ArgumentNullException("element"); 
            } 

            return GetValue(element, CommentsProperty); 
        }

        /// <summary>
        /// Writes the attached property Comments to the given element. 
        /// </summary>
        /// <param name="element">The element to which to write the attached property.</param> 
        /// <param name="comments">The property value to set</param> 
        public static void SetComments(object element, string comments)
        { 
            if (element == null)
            {
                throw new ArgumentNullException("element");
            } 

            LocComments.ParsePropertyComments(comments); 
            SetValue(element, CommentsProperty, comments); 
        }
 
        /// <summary>
        /// Reads the attached property Localizability from given element.
        /// </summary>
        /// <param name="element">The element from which to read the attached property.</param> 
        /// <returns>The property's value.</returns>
        [AttachedPropertyBrowsableForType(typeof(object))] 
        public static string GetAttributes(object element) 
        {
            if (element == null) 
            {
                throw new ArgumentNullException("element");
            }
 
            return GetValue(element, AttributesProperty);
        } 
 
        /// <summary>
        /// Writes the attached property Localizability to the given element. 
        /// </summary>
        /// <param name="element">The element to which to write the attached property.</param>
        /// <param name="attributes">The property value to set</param>
        public static void SetAttributes(object element, string attributes) 
        {
            if (element == null) 
            { 
                throw new ArgumentNullException("element");
            } 

            LocComments.ParsePropertyLocalizabilityAttributes(attributes);
            SetValue(element, AttributesProperty, attributes);
        } 

        private static string GetValue(object element, DependencyProperty property) 
        { 
            DependencyObject dependencyObject = element as DependencyObject;
            if (dependencyObject != null) 
            {
                // For DO, get the value from the property system
                return (string) dependencyObject.GetValue(property);
            } 

            // For objects, get the value from our own hashtable 
            if (property == CommentsProperty) 
            {
                lock(_commentsOnObjects.SyncRoot) 
                {
                    return (string) _commentsOnObjects[element];
                }
            } 
            else
            { 
                Debug.Assert(property == AttributesProperty); 
                lock(_attributesOnObjects.SyncRoot)
                { 
                    return (string) _attributesOnObjects[element];
                }
            }
        } 

        private static void SetValue(object element, DependencyProperty property, string value) 
        { 
            DependencyObject dependencyObject = element as DependencyObject;
            if (dependencyObject != null) 
            {
                // For DO, store the value in the property system
                dependencyObject.SetValue(property, value);
                return; 
            }
 
            // For other objects, store the value in our own hashtable 
            if (property == CommentsProperty)
            { 
                lock(_commentsOnObjects.SyncRoot)
                {
                    _commentsOnObjects[element] = value;
                } 
            }
            else 
            { 
                Debug.Assert(property == AttributesProperty);
                lock(_attributesOnObjects.SyncRoot) 
                {
                    _attributesOnObjects[element] = value;
                }
            } 
        }
 
 
        ///
        /// private storage for values set on objects 
        ///
        private static Hashtable _commentsOnObjects = new Hashtable();
        private static Hashtable _attributesOnObjects = new Hashtable();
    } 
}

