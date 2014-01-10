//---------------------------------------------------------------------------- 
//
// <copyright file="ValueTable.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Mapping of (item, property accessor) to value 
// 
//---------------------------------------------------------------------------
 
/***************************************************************************\
    Some properties in the world behave poorly, in the sense that two
    successive GetValue calls return different values (i.e. the property
    "changes" every time you call it).  To work around this problem, we 
    cache the value the first time we retrieve it, and interpose a cache
    lookup in front of every GetValue.  The ValueTable class implements this 
    cache;  there is one such table per DataBindEngine (hence one per Dispatcher). 

    The malfeasant properties seem to be isolated to ADO-related classes in 
    System.Data.  Lacking an explicit list of all such properties, or a rule
    for identifying them, we use a heuristic that covers all the known cases.
    See the ShouldCache method.
\***************************************************************************/ 

using System; 
using System.Collections; 
using System.Collections.Generic;           // IEnumerable<T>
using System.Collections.Specialized;       // HybridDictionary 
using System.ComponentModel;                // IBindingList
using System.Reflection;                    // TypeDescriptor
using System.Windows;                       // SR
using MS.Internal;                          // Invariant.Assert 

namespace MS.Internal.Data 
{ 
    internal sealed class ValueTable : IWeakEventListener
    { 
        // should we cache the value of the given property from the given item?
        internal static bool ShouldCache(object item, PropertyDescriptor pd)
        {
            // custom property descriptors returning IBindingList (bug 1190076) 
            if (AssemblyHelper.IsDataSetCollectionProperty(pd))
            { 
                return true; 
            }
 
            // XLinq's property descriptors for the Elements and Descendants properties
            if (AssemblyHelper.IsXLinqCollectionProperty(pd))
            {
                return true; 
            }
 
            return false;       // everything else is treated normally 
        }
 
        // retrieve the value, using the cache if necessary
        internal object GetValue(object item, PropertyDescriptor pd, bool indexerIsNext)
        {
            if (!ShouldCache(item, pd)) 
            {
                // normal case - just get the value the old-fashioned way 
                return pd.GetValue(item); 
            }
            else 
            {
                // lazy creation of the cache
                if (_table == null)
                { 
                    _table = new HybridDictionary();
                } 
 
                // look up the value in the cache
                bool isXLinqCollectionProperty = AssemblyHelper.IsXLinqCollectionProperty(pd); 
                ValueTableKey key = new ValueTableKey(item, pd);
                object value = _table[key];

                // if there's no entry, fetch the value and cache it 
                if (value == null)
                { 
                    if (isXLinqCollectionProperty) 
                    {
                        // interpose our own value for special XLinq properties 
                        value = new XDeferredAxisSource(item, pd);
                    }
                    else
                    { 
                        value = pd.GetValue(item);
                    } 
 
                    if (value == null)
                    { 
                        value = CachedNull;     // distinguish a null value from no entry
                    }

                    _table[key] = value; 
                }
 
                // decode null, if necessary 
                if (value == CachedNull)
                { 
                    value = null;
                }
                else if (isXLinqCollectionProperty && !indexerIsNext)
                { 
                    // The XLinq properties need special help.  When the path
                    // contains "Elements[Foo]", we should return the interposed 
                    // XDeferredAxisSource;  the path worker will then call the XDAS's 
                    // indexer with argument "Foo", and obtain the desired
                    // ObservableCollection.  But when the path contains "Elements" 
                    // with no indexer, we should return an ObservableCollection
                    // corresponding to the full set of children.
                    // [All this applies to "Descendants" as well.]
                    XDeferredAxisSource xdas = (XDeferredAxisSource)value; 
                    value = xdas.FullCollection;
                } 
 
                return value;
            } 
        }

        // listen for changes to a property
        internal void RegisterForChanges(object item, PropertyDescriptor pd, DataBindEngine engine) 
        {
            // lazy creation of the cache 
            if (_table == null) 
            {
                _table = new HybridDictionary(); 
            }

            ValueTableKey key = new ValueTableKey(item, pd);
            object value = _table[key]; 

            if (value == null) 
            { 
                // new entry needed - add a listener
                INotifyPropertyChanged inpc = item as INotifyPropertyChanged; 
                if (inpc != null)
                {
                    PropertyChangedEventManager.AddListener(inpc, this, pd.Name);
                } 
                else
                { 
                    ValueChangedEventManager.AddListener(item, this, pd); 
                }
            } 
        }

        /// <summary>
        /// Handle events from the centralized event table 
        /// </summary>
        bool IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e) 
        { 
            if (managerType == typeof(PropertyChangedEventManager))
            { 
                string propertyName = ((PropertyChangedEventArgs)e).PropertyName;
                if (propertyName == null)   // normalize - null and empty mean the same
                {
                    propertyName = String.Empty; 
                }
                InvalidateCache(sender, propertyName); 
            } 
            else if (managerType == typeof(ValueChangedEventManager))
            { 
                ValueChangedEventArgs vce = (ValueChangedEventArgs)e;
                InvalidateCache(sender, vce.PropertyDescriptor);
            }
            else 
            {
                return false;       // unrecognized event 
            } 

            return true; 
        }

        // invalidate (remove) a cache entry.  Called when the source raises a change event.
        void InvalidateCache(object item, string name) 
        {
            // when name is empty, invalidate all properties for the given item 
            if (name == String.Empty) 
            {
                foreach (PropertyDescriptor pd1 in GetPropertiesForItem(item)) 
                {
                    InvalidateCache(item, pd1);
                }
                return; 
            }
 
            // regenerate the descriptor from the name 
            // (this code matches PropertyPathWorker.GetInfo)
            PropertyDescriptor pd; 
            if (item is ICustomTypeDescriptor)
            {
                pd = TypeDescriptor.GetProperties(item)[name];
            } 
            else
            { 
                pd = TypeDescriptor.GetProperties(item.GetType())[name]; 
            }
 
            InvalidateCache(item, pd);
        }

 
        // invalidate (remove) a cache entry.  Called when the source raises a change event.
        void InvalidateCache(object item, PropertyDescriptor pd) 
        { 
            // ignore changes to special XLinq PD's - leave our interposed object in the cache
            if (AssemblyHelper.IsXLinqCollectionProperty(pd)) 
                return;

            ValueTableKey key = new ValueTableKey(item, pd);
            _table.Remove(key); 
        }
 
        // return all the properties registered for the given item 
        IEnumerable<PropertyDescriptor> GetPropertiesForItem(object item)
        { 
            List<PropertyDescriptor> result = new List<PropertyDescriptor>();

            foreach (DictionaryEntry de in _table)
            { 
                ValueTableKey key = (ValueTableKey)de.Key;
                if (Object.Equals(item, key.Item)) 
                { 
                    result.Add(key.PropertyDescriptor);
                } 
            }

            return result;
        } 

        // remove stale entries from the table 
        internal bool Purge() 
        {
            if (_table == null) 
                return false;

            // first see if there are any stale entries.  No sense allocating
            // storage if there's nothing to do. 
            bool isPurgeNeeded = false;
            ICollection keys = _table.Keys; 
            foreach (ValueTableKey key in keys) 
            {
                if (key.IsStale) 
                {
                    isPurgeNeeded = true;
                    break;
                } 
            }
 
            // if the purge is needed, copy the keys and purge the 
            // stale entries.  The copy avoids deletion out from under the
            // key collection. 
            if (isPurgeNeeded)
            {
                ValueTableKey[] localKeys = new ValueTableKey[keys.Count];
                keys.CopyTo(localKeys, 0); 

                for (int i=localKeys.Length-1;  i >= 0;  --i) 
                { 
                    if (localKeys[i].IsStale)
                    { 
                        _table.Remove(localKeys[i]);
                    }
                }
            } 

            return isPurgeNeeded;   // return true if something happened 
        } 

        private HybridDictionary _table; 
        private static object CachedNull = new Object();

        private class ValueTableKey
        { 
            public ValueTableKey(object item, PropertyDescriptor pd)
            { 
                Invariant.Assert(item != null && pd != null); 

                // store weak references to item and pd, so as not to affect their 
                // GC behavior.  But remember their hashcode.
                _item = new WeakReference(item);
                _descriptor = new WeakReference(pd);
                _hashCode = unchecked(item.GetHashCode() + pd.GetHashCode()); 
            }
 
            public object Item 
            {
                get { return _item.Target; } 
            }

            public PropertyDescriptor PropertyDescriptor
            { 
                get { return (PropertyDescriptor)_descriptor.Target; }
            } 
 
            public bool IsStale
            { 
                get { return Item == null || PropertyDescriptor == null; }
            }

            public override bool Equals(object o) 
            {
                if (o == this) 
                    return true;    // this allows deletion of stale keys 

                ValueTableKey that = o as ValueTableKey; 
                if (that != null)
                {
                    object item = this.Item;
                    PropertyDescriptor descriptor = this.PropertyDescriptor; 
                    if (item == null || descriptor == null)
                        return false;   // a stale key matches nothing (except itself) 
 
                    return this._hashCode == that._hashCode &&
                            Object.Equals(item, that.Item) && 
                            Object.Equals(descriptor, that.PropertyDescriptor);
                }

                return false;   // this doesn't match a non-ValueTableKey 
            }
 
            public override int GetHashCode() 
            {
                return _hashCode; 
            }

            WeakReference _item;
            WeakReference _descriptor; 
            int _hashCode;
        } 
    } 
}
