//---------------------------------------------------------------------------- 
//
// <copyright file="AssemblyHelper.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: services for code that potentially loads uncommon assemblies. 
// 
//---------------------------------------------------------------------------
 
/*
    Most of the WPF codebase uses types from WPF's own assemblies or from certain
    standard .Net assemblies (System, mscorlib, etc.).   However, some code uses
    types from other assemblies (System.Xml, System.Data, etc.) - we'll refer to 
    these as "uncommon" assemblies.   We don't want WPF to load an uncommon assembly
    unless the app itself needs to. 
 
    The AssemblyHelper class helps to solve this problem by keeping track of which
    uncommon assemblies have been loaded.  Any code that uses an uncommon assembly 
    should be isolated in a separate method marked with the NoInlining attribute,
    and calls to that method should be protected by testing AssemblyHelper.IsLoaded.

    Many such methods are provided in this class (see "Convenience methods for ..."). 
    These are simple methods that don't need state from another WPF class.  More
    complex methods are defined elsewhere in WPF, as appropriate. 
*/ 

using System; 
using System.ComponentModel;        // PropertyDescriptor
using System.Diagnostics;           // Debug.Assert
using System.Reflection;            // Assembly
using System.Security;              // [SecurityCritical] 
using SRC=System.Runtime.CompilerServices;  // NoInlining
 
using MS.Internal.WindowsBase;      // [FriendAccessAllowed] 

using System.Drawing; 
using System.Drawing.Imaging;
using System.Xml;
using System.Xml.Linq;
using System.Data; 
using System.Data.SqlTypes;
using System.Dynamic; 
 
namespace MS.Internal
{ 
    [FriendAccessAllowed]
    internal enum UncommonAssembly
    {
        // Each enum name must match the assembly name, with dots replaced by underscores 
        System_Drawing,
        System_Xml, 
        System_Xml_Linq, 
        System_Data,
        System_Core, 
    }

    [FriendAccessAllowed]
    internal static class AssemblyHelper 
    {
        #region Constructors 
 
        /// <SecurityNote>
        ///     Critical: accesses AppDomain.AssemblyLoad event 
        ///     TreatAsSafe: the event is not exposed - merely updates internal state.
        /// </SecurityNote>
        [SecurityCritical,SecurityTreatAsSafe]
        static AssemblyHelper() 
        {
            // create the records for each uncommon assembly 
            string[] names = Enum.GetNames(typeof(UncommonAssembly)); 
            int n = names.Length;
            _records = new AssemblyRecord[n]; 

            for (int i=0; i<n; ++i)
            {
                _records[i].Name = names[i].Replace('_','.') + ",";  // comma delimits simple name within Assembly.FullName 
            }
 
            // register for AssemblyLoad event 
            AppDomain domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad += OnAssemblyLoad; 

            // handle the assemblies that are already loaded
            Assembly[] assemblies = domain.GetAssemblies();
            for (int i=assemblies.Length-1;  i>=0;  --i) 
            {
                OnLoaded(assemblies[i]); 
            } 
        }
 
        #endregion Constructors

        #region Internal Methods
 
        /// <SecurityNote>
        ///     Critical: accesses critical field _records 
        ///     TreatAsSafe: it's OK to read the IsLoaded bit 
        /// </SecurityNote>
        [SecurityCritical,SecurityTreatAsSafe] 
        [FriendAccessAllowed]
        internal static bool IsLoaded(UncommonAssembly assemblyEnum)
        {
            // this method is typically called by WPF code on a UI thread. 
            // Although assemblies can load on any thread, there's no need to lock.
            // If the object of interest came from the given assembly, the 
            // AssemblyLoad event has already been raised and the bit has already 
            // been set before the caller calls IsLoaded.
            return _records[(int)assemblyEnum].IsLoaded; 
        }

        #endregion Internal Methods
 
        #region Convenience methods for System.Drawing
 
        // return true if the data is a bitmap 
        internal static bool IsBitmap(object data)
        { 
            return IsLoaded(UncommonAssembly.System_Drawing)
                    ? IsBitmapImpl(data)
                    : false;
        } 

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        private static bool IsBitmapImpl(object data) 
        {
            return data is Bitmap; 
        }

        // return true if the data is an Image
        internal static bool IsImage(object data) 
        {
            return IsLoaded(UncommonAssembly.System_Drawing) 
                    ? IsImageImpl(data) 
                    : false;
        } 

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsImageImpl(object data)
        { 
            return data is Image;
        } 
 
        // return true if the data is a graphics metafile
        internal static bool IsMetafile(object data) 
        {
            return IsLoaded(UncommonAssembly.System_Drawing)
                    ? IsMetafileImpl(data)
                    : false; 
        }
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        private static bool IsMetafileImpl(object data)
        { 
            return data is Metafile;
        }

        /// <SecurityNote> 
        ///     Critical:  This code returns a handle to an unmanaged object
        /// </SecurityNote> 
        [SecurityCritical] 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        internal static IntPtr GetHandleFromMetafile(Object data) 
        {
            IntPtr hMetafile = IntPtr.Zero;
            Metafile metafile = data as Metafile;
 
            if (metafile != null)
            { 
                // Get the Windows handle from the metafile object. 
                hMetafile = metafile.GetHenhmetafile();
            } 

            return hMetafile;
        }
 
        // Get the metafile from the handle of the enhanced metafile.
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        internal static Object GetMetafileFromHemf(IntPtr hMetafile) 
        {
            return new Metafile(hMetafile, false); 
        }

        #endregion Convenience methods for System.Drawing
 
        #region Convenience methods for System.Xml
 
        // return true if the item is an XmlNode 
        internal static bool IsXmlNode(object item)
        { 
            return IsLoaded(UncommonAssembly.System_Xml)
                    ? IsXmlNodeImpl(item)
                    : false;
        } 

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        private static bool IsXmlNodeImpl(object item) 
        {
            return item is XmlNode; 
        }

        // return true if the item is an XmlNamespaceManager
        internal static bool IsXmlNamespaceManager(object item) 
        {
            return IsLoaded(UncommonAssembly.System_Xml) 
                    ? IsXmlNamespaceManagerImpl(item) 
                    : false;
        } 

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsXmlNamespaceManagerImpl(object item)
        { 
            return item is XmlNamespaceManager;
        } 
 
        // if the item is an XmlNode, get the value corresponding to the given name
        internal static bool TryGetValueFromXmlNode(object item, string name, out object value) 
        {
            if (IsLoaded(UncommonAssembly.System_Xml))
            {
                return TryGetValueFromXmlNodeImpl(item, name, out value); 
            }
            else 
            { 
                value = null;
                return false; 
            }
        }

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        private static bool TryGetValueFromXmlNodeImpl(object item, string name, out object value)
        { 
            XmlNode node = item as XmlNode; 
            if (node != null)
            { 
                value = SelectStringValue(node, name, null);
                return true;
            }
            else 
            {
                value = null; 
                return false; 
            }
        } 

        // Return a string by applying an XPath query to an XmlNode.
        internal static string SelectStringValue(XmlNode node, string query, XmlNamespaceManager namespaceManager)
        { 
            string strValue;
            XmlNode result; 
 
            result = node.SelectSingleNode(query, namespaceManager);
 
            if (result != null)
            {
                strValue = ExtractString(result);
            } 
            else
            { 
                strValue = String.Empty; 
            }
 
            return strValue;
        }

 
        /// <summary>
        /// Get a string from an XmlNode (of any kind:  element, attribute, etc.) 
        /// </summary> 
        private static string ExtractString(XmlNode node)
        { 
            string value = "";

            if (node.NodeType == XmlNodeType.Element)
            { 
                for (int i = 0; i < node.ChildNodes.Count; i++)
                { 
                    if (node.ChildNodes[i].NodeType == XmlNodeType.Text) 
                    {
                        value += node.ChildNodes[i].Value; 
                    }
                }
            }
            else 
            {
                value = node.Value; 
            } 
            return value;
        } 

        #endregion Convenience methods for System.Xml

        #region Convenience methods for System.Xml.Linq 

        // return true if the item is an XElement 
        internal static bool IsXElement(object item) 
        {
            return IsLoaded(UncommonAssembly.System_Xml_Linq) 
                    ? IsXElementImpl(item)
                    : false;
        }
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsXElementImpl(object item) 
        { 
            return item is XElement;
        } 

        // return a string of the form "{http://my.namespace}TagName"
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        internal static string GetXElementTagName(object item) 
        {
            XName name = ((XElement)item).Name; 
            return (name != null) ? name.ToString() : null; 
        }
 
        // XLinq exposes two synthetic properties - Elements and Descendants -
        // on XElement that return IEnumerable<XElement>.  We handle these specially
        // to work around problems involving identity and change notifications
        internal static bool IsXLinqCollectionProperty(PropertyDescriptor pd) 
        {
            return IsLoaded(UncommonAssembly.System_Xml_Linq) 
                    ? IsXLinqCollectionPropertyImpl(pd) 
                    : false;
        } 

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        static bool IsXLinqCollectionPropertyImpl(PropertyDescriptor pd)
        { 
            if (s_XElementElementsPropertyDescriptorType == null)
            { 
                // lazy load the types for the two offending PD's.  They're internal, so 
                // we get them indirectly.
                XElement xelement = new XElement("Dummy"); 
                PropertyDescriptorCollection pdc = TypeDescriptor.GetProperties(xelement);
                s_XElementElementsPropertyDescriptorType = pdc["Elements"].GetType();
                s_XElementDescendantsPropertyDescriptorType = pdc["Descendants"].GetType();
            } 

            Type pdType = pd.GetType(); 
            return (pdType == s_XElementElementsPropertyDescriptorType) || 
                (pdType == s_XElementDescendantsPropertyDescriptorType);
        } 

        private static Type s_XElementElementsPropertyDescriptorType;
        private static Type s_XElementDescendantsPropertyDescriptorType;
 
        #endregion Convenience methods for System.Xml.Linq
 
        #region Convenience methods for System.Data 

        // return true if the item is a DataRowView 
        internal static bool IsDataRowView(object item)
        {
            return IsLoaded(UncommonAssembly.System_Data)
                        ? IsDataRowViewImpl(item) 
                        : false;
        } 
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsDataRowViewImpl(object item) 
        {
            return (item is DataRowView);
        }
 
        // return true if the value is null in the SqlTypes sense
        internal static bool IsSqlNull(object value) 
        { 
            return IsLoaded(UncommonAssembly.System_Data)
                        ? IsSqlNullImpl(value) 
                        : false;
        }

        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)] 
        private static bool IsSqlNullImpl(object value)
        { 
            INullable nullable = value as INullable; 
            return (nullable != null && nullable.IsNull);
        } 

        // return true if the type is nullable in the SqlTypes sense
        internal static bool IsSqlNullableType(Type type)
        { 
            return IsLoaded(UncommonAssembly.System_Data)
                        ? IsSqlNullableTypeNullImpl(type) 
                        : false; 
        }
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsSqlNullableTypeNullImpl(Type type)
        {
            return typeof(INullable).IsAssignableFrom(type); 
        }
 
        // return a null value appropriate for the given SqlNullable type 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        internal static object NullValueForSqlNullableType(Type type) 
        {
            // some SqlTypes are classes with a Null property.  Others are structs with a Null field.  Try both.
            FieldInfo nullField = type.GetField("Null", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (nullField != null) 
            {
                return nullField.GetValue(null); 
            } 

            PropertyInfo nullProperty = type.GetProperty("Null", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy); 
            if (nullProperty != null)
            {
                return nullProperty.GetValue(null, null);
            } 

            Debug.Assert(false, "Could not find Null field or property for SqlNullable type"); 
            return null; 
        }
 
        // ADO DataSet exposes some properties that cause problems involving
        // identity and change notifications.  We handle these specially.
        internal static bool IsDataSetCollectionProperty(PropertyDescriptor pd)
        { 
            return IsLoaded(UncommonAssembly.System_Data)
                    ? IsDataSetCollectionPropertyImpl(pd) 
                    : false; 
        }
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        static bool IsDataSetCollectionPropertyImpl(PropertyDescriptor pd)
        {
            if (s_DataTablePropertyDescriptorType == null) 
            {
                // lazy load the types for the offending PD's.  They're internal, so 
                // we get them indirectly. 
                DataSet dataset = new DataSet();
                dataset.Locale = System.Globalization.CultureInfo.InvariantCulture; 

                DataTable table1 = new DataTable("Table1");
                table1.Locale = System.Globalization.CultureInfo.InvariantCulture;
                table1.Columns.Add("ID", typeof(int)); 
                dataset.Tables.Add(table1);
 
                DataTable table2 = new DataTable("Table2"); 
                table2.Locale = System.Globalization.CultureInfo.InvariantCulture;
                table2.Columns.Add("ID", typeof(int)); 
                dataset.Tables.Add(table2);

                dataset.Relations.Add(new DataRelation("IDRelation",
                                                    table1.Columns["ID"], 
                                                    table2.Columns["ID"]));
 
                System.Collections.IList list = ((IListSource)dataset).GetList(); 
                PropertyDescriptorCollection pdc = TypeDescriptor.GetProperties(list[0]);
                s_DataTablePropertyDescriptorType = pdc["Table1"].GetType(); 

                pdc = ((ITypedList)table1.DefaultView).GetItemProperties(null);
                s_DataRelationPropertyDescriptorType = pdc["IDRelation"].GetType();
            } 

            Type pdType = pd.GetType(); 
            return (pdType == s_DataTablePropertyDescriptorType) || 
                (pdType == s_DataRelationPropertyDescriptorType);
        } 

        static Type s_DataTablePropertyDescriptorType;
        static Type s_DataRelationPropertyDescriptorType;
 
        #endregion Convenience methods for System.Data
 
        #region Convenience methods for System.Core 

        // return true if the item implements IDynamicMetaObjectProvider 
        internal static bool IsIDynamicMetaObjectProvider(object item)
        {
            return IsLoaded(UncommonAssembly.System_Core)
                        ? IsIDynamicMetaObjectProviderImpl(item) 
                        : false;
        } 
 
        [SRC.MethodImplAttribute(SRC.MethodImplOptions.NoInlining)]
        private static bool IsIDynamicMetaObjectProviderImpl(object item) 
        {
            return (item is IDynamicMetaObjectProvider);
        }
 
        #endregion Convenience methods for System.Core
 
        #region Private Methods 

        /// <SecurityNote> 
        ///     Critical:  This code potentially sets the IsLoaded bit for the given assembly.
        /// </SecurityNote>
        [SecurityCritical]
        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) 
        {
            OnLoaded(args.LoadedAssembly); 
        } 

        /// <SecurityNote> 
        ///     Critical:  This code potentially sets the IsLoaded bit for the given assembly.
        /// </SecurityNote>
        [SecurityCritical]
        private static void OnLoaded(Assembly assembly) 
        {
            // although this method can be called on an arbitrary thread, there's no 
            // need to lock.  The only change it makes is a monotonic one - changing 
            // a bit in an AssemblyRecord from false to true.  Even if two threads try
            // to do this simultaneously, the same outcome results. 

            // ignore reflection-only assemblies - we care about running code from the assembly
            if (assembly.ReflectionOnly)
                return; 

            // see if the assembly matches one of the uncommon assemblies 
            for (int i=_records.Length-1; i>=0; --i) 
            {
                if (!_records[i].IsLoaded && 
                    assembly.GlobalAssemblyCache &&
                    assembly.FullName.StartsWith(_records[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    _records[i].IsLoaded = true; 
                }
            } 
        } 

        #endregion Private Methods 

        #region Private Data

        private struct AssemblyRecord 
        {
            public string Name { get; set; } 
            public bool IsLoaded { get; set; } 
        }
 
        /// <SecurityNote>
        ///     Critical:   The IsLoaded status could be used in security-critical
        ///                 situations.  Make sure the IsLoaded bit is only set by authorized
        ///                 code, namely OnLoaded. 
        /// </SecurityNote>
        [SecurityCritical] 
        private static AssemblyRecord[] _records; 

        #endregion Private Data 
    }
}

