//------------------------------------------------------------------------ 
//
//  Microsoft Windows Client Platform
//  Copyright (C) Microsoft Corporation. All rights reserved.
// 
//-----------------------------------------------------------------------
 
using System; 
using System.Collections;
using System.Collections.Generic; 
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Markup; 
using System.Globalization;
 
namespace System.Windows.Markup.Primitives 
{
    /// <summary> 
    /// Utility class use as a base class for classes wrap another
    /// instance by delegating MarkupItem implementation to that
    /// instance.
    /// </summary> 
    internal class MarkupObjectWrapper : MarkupObject
    { 
        MarkupObject  _baseObject; 

        public MarkupObjectWrapper(MarkupObject baseObject) 
        {
            _baseObject = baseObject;
        }
 
        public override void AssignRootContext(IValueSerializerContext context)
        { 
            _baseObject.AssignRootContext(context); 
        }
 
        public override AttributeCollection Attributes
        {
            get { return _baseObject.Attributes;  }
        } 

        public override Type ObjectType 
        { 
            get { return _baseObject.ObjectType; }
        } 

        public override object Instance
        {
            get { return _baseObject.Instance; } 
        }
 
        internal override IEnumerable<MarkupProperty> GetProperties(bool mapToConstructorArgs) 
        {
            return _baseObject.GetProperties(mapToConstructorArgs); 
        }
    }

    /// <summary> 
    /// Utility class use as a base class for classes wrap another
    /// instance by delegating MarkupProperty implementation to that 
    /// instance. 
    /// </summary>
    internal class MarkupPropertyWrapper : MarkupProperty 
    {
        MarkupProperty _baseProperty;

        /* 
        protected MarkupProperty BaseProperty
        { 
            get { return _baseProperty; } 
        }
        */ 

        public MarkupPropertyWrapper(MarkupProperty baseProperty)
        {
            _baseProperty = baseProperty; 
        }
 
        public override AttributeCollection Attributes 
        {
            get { return _baseProperty.Attributes; } 
        }

        public override IEnumerable<MarkupObject> Items
        { 
            get { return _baseProperty.Items; }
        } 
 
        public override string Name
        { 
            get { return _baseProperty.Name; }
        }

        public override Type PropertyType 
        {
            get { return _baseProperty.PropertyType; } 
        } 

        public override string StringValue 
        {
            get { return _baseProperty.StringValue; }
        }
 
        public override IEnumerable<Type> TypeReferences
        { 
            get { return _baseProperty.TypeReferences; } 
        }
 
        public override object Value
        {
            get { return _baseProperty.Value; }
        } 

        public override DependencyProperty DependencyProperty 
        { 
            get { return _baseProperty.DependencyProperty; }
        } 

        public override bool IsAttached
        {
            get { return _baseProperty.IsAttached; } 
        }
 
        public override bool IsComposite 
        {
            get { return _baseProperty.IsComposite; } 
        }

        public override bool IsConstructorArgument
        { 
            get { return _baseProperty.IsConstructorArgument; }
        } 
 
        public override bool IsKey
        { 
            get { return _baseProperty.IsKey; }
        }

        public override bool IsValueAsString 
        {
            get { return _baseProperty.IsValueAsString; } 
        } 

        public override bool IsContent 
        {
            get { return _baseProperty.IsContent; }
        }
 
        public override PropertyDescriptor PropertyDescriptor
        { 
            get { return _baseProperty.PropertyDescriptor; } 
        }
 
        /// <summary>
        /// Checks to see that each markup object is of a public type.  Used in serialization.
        ///
        /// This implementation just checks the base property. 
        /// </summary>
        internal override void VerifyOnlySerializableTypes() 
        { 
            _baseProperty.VerifyOnlySerializableTypes();
        } 
    }

    /// <summary>
    /// A MarkupItem wrapper that creates an ExtensionSimplifierProperty wrapper 
    /// for every property returned. All other implementation is delegated to
    /// the wrapped item. 
    /// </summary> 
    internal class ExtensionSimplifierMarkupObject : MarkupObjectWrapper
    { 
        IValueSerializerContext _context;

        public ExtensionSimplifierMarkupObject(MarkupObject baseObject, IValueSerializerContext context)
            : base(baseObject) 
        {
            _context = context; 
        } 

        /// This is placed in its own method to avoid accessing base.Properties from the 
        /// iterator class generated by the code below because C# produces unverifiable
        /// code for the expression.
        private IEnumerable<MarkupProperty> GetBaseProperties(bool mapToConstructorArgs) {
            return base.GetProperties(mapToConstructorArgs); 
        }
 
        internal override IEnumerable<MarkupProperty> GetProperties(bool mapToConstructorArgs) 
        {
            foreach (MarkupProperty property in GetBaseProperties(mapToConstructorArgs)) 
            {
                yield return new ExtensionSimplifierProperty(property, _context);
            }
        } 

        public override void AssignRootContext(IValueSerializerContext context) 
        { 
            _context = context;
            base.AssignRootContext(context); 
        }
    }

    /// <summary> 
    /// A MarkupProperty wrapper that creates simplifies items for objects
    /// of type MarkupExtension to a string if all its properties can be 
    /// simplified into a string. This is recursive in that a markup extension 
    /// can contain references to other markup extensions which are themselves
    /// simplified. 
    /// </summary>
    internal class ExtensionSimplifierProperty : MarkupPropertyWrapper
    {
        IValueSerializerContext _context; 

        public ExtensionSimplifierProperty(MarkupProperty baseProperty, IValueSerializerContext context) : base(baseProperty) 
        { 
            _context = context;
        } 

        public override bool IsComposite
        {
            get 
            {
                // See if we can convert an extension into a string. 
                if (!base.IsComposite) 
                {
                    // If it is already a string then we can. 
                    return false;
                }

                // If the property is a collection, this property is a composite. 
                if (IsCollectionProperty)
                { 
                    return true; 
                }
 
                bool first = true;
                foreach (MarkupObject item in Items)
                {
                    // If there is more than one MarkupExtension, we can't. 
                    // If it is not a markup extension we can't.
                    if (!first || 
                        !typeof(MarkupExtension).IsAssignableFrom(item.ObjectType)) 
                    {
                        return true; 
                    }
                    first = false;

                    // If any of the properties are composite we can't. This is recursive to this 
                    // routine because of the wrapping below.
                    item.AssignRootContext(_context); 
                    foreach (MarkupProperty property in item.Properties) 
                    {
                        if (property.IsComposite) 
                        {
                            return true;
                        }
                    } 
                }
 
                // We can turn this into a string if we have seen at least one item 
                return first;
            } 
        }

        /// This is placed in its own method to avoid accessing base.Items from the
        /// iterator class generated by the code below because C# produces unverifiable 
        /// code for the expression.
        private IEnumerable<MarkupObject> GetBaseItems() 
        { 
            return base.Items;
        } 

        public override IEnumerable<MarkupObject> Items
        {
            get 
            {
                // Wrap all of the items from the property we are wrapping. 
                foreach (MarkupObject baseItem in GetBaseItems()) 
                {
                    ExtensionSimplifierMarkupObject item = new ExtensionSimplifierMarkupObject(baseItem, _context); 
                    item.AssignRootContext(_context);
                    yield return item;
                }
            } 
        }
 
        private const int EXTENSIONLENGTH = 9; // the number of characters in the string "Extension" 

        public override string StringValue 
        {
            get
            {
                string result = null; 

                if (!base.IsComposite) 
                { 
                    // Escape the text as necessary to avoid being mistaken for a MarkupExtension.
                    result = MarkupExtensionParser.AddEscapeToLiteralString(base.StringValue); 
                }
                else
                {
                    // Convert the markup extension into a string 
                    foreach (MarkupObject item in Items)
                    { 
                        result = ConvertMarkupItemToString(item); 
                        break;
                    } 

                    if (result == null)
                    {
                        Debug.Fail("No items where found and IsComposite return true"); 
                        result = "";
                    } 
                } 
                return result;
            } 
        }

        private string ConvertMarkupItemToString(MarkupObject item)
        { 
            ValueSerializer typeSerializer = _context.GetValueSerializerFor(typeof(Type));
            Debug.Assert(typeSerializer != null, "Could not retrieve typeSerializer for Type"); 
 
            // Serialize the markup extension into a string
            StringBuilder resultBuilder = new StringBuilder(); 
            resultBuilder.Append('{');

            string typeName = typeSerializer.ConvertToString(item.ObjectType, _context);
 
            if (typeName.EndsWith("Extension", StringComparison.Ordinal))
            { 
                // The "Extension" suffix is optional, much like the Attribute suffix of an Attribute. 
                // The normalized version is without the suffix.
                resultBuilder.Append(typeName, 0, typeName.Length - EXTENSIONLENGTH); 
            }
            else
            {
                resultBuilder.Append(typeName); 
            }
 
            bool first = true; 
            bool propertyWritten = false;
 
            foreach (MarkupProperty property in item.Properties)
            {
                resultBuilder.Append(first ? " " : ", ");
 
                first = false;
                if (!property.IsConstructorArgument) 
                { 
                    resultBuilder.Append(property.Name);
                    resultBuilder.Append('='); 
                    propertyWritten = true;
                }
                else
                { 
                    Debug.Assert(!propertyWritten, "An argument was returned after a property was set. All arguments must be returned first and in order");
                } 
 
                string value = property.StringValue;
 
                if (value != null && value.Length > 0)
                {
                    if (value[0] == '{')
                    { 
                        if (value.Length > 1 && value[1] == '}')
                        { 
                            // It is a literal quote, remove the literals and write the text with escapes. 
                            value = value.Substring(2);
                        } 
                        else
                        {
                            // It is a nested markup-extension, just insert the text literally.
                            resultBuilder.Append(value); 
                            continue;
                        } 
                    } 

                    // Escape the string 
                    for (int i = 0; i < value.Length; i++)
                    {
                        char ch = value[i];
                        switch (ch) 
                        {
                            case '{': 
                                resultBuilder.Append(@"\{"); 
                                break;
                            case '}': 
                                resultBuilder.Append(@"\}");
                                break;
                            case ',':
                                resultBuilder.Append(@"\,"); 
                                break;
                            default: 
                                resultBuilder.Append(ch); 
                                break;
                        } 
                    }
                }
            }
            resultBuilder.Append('}'); 

            return resultBuilder.ToString(); 
        } 

        /// <summary> 
        /// Checks to see that each markup object is of a public type.  Used in serialization.
        ///
        /// This implementation checks the base property, checks the item's type, and recursively checks each of
        /// the item's properties. 
        /// </summary>
        internal override void VerifyOnlySerializableTypes() 
        { 
            base.VerifyOnlySerializableTypes();
 
            if(base.IsComposite)
            {
                foreach (MarkupObject item in Items)
                { 
                    MarkupWriter.VerifyTypeIsSerializable(item.ObjectType);
 
                    foreach (MarkupProperty property in item.Properties) 
                    {
                        property.VerifyOnlySerializableTypes(); 
                    }
                }
            }
        } 
    }
} 

