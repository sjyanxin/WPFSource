//------------------------------------------------------------------------------ 
//
// <copyright file="ReadOnlyDictionary.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//  This class defines a generic Read Only dictionary 
//
// History: 
//  04/19/2005: LGolding:   Initial implementation.
//  03/06/2006: IgorBel:   Switch from the RM specific Use license dictionary to a generic Read Only dictionary that can
//                                be share across multiple scenarios
// 
//-----------------------------------------------------------------------------
 
using System; 
using System.Collections;
using System.Collections.Generic; 

//using System.Collections.Specialized;

using System.Windows;   // for resources 
using MS.Internal;          // for invariant
 
using SR=MS.Internal.WindowsBase.SR; 
using SRID=MS.Internal.WindowsBase.SRID;
 
namespace MS.Internal.Utility
{
    /// <summary>
    ///  This is a generic Read Only Dictionary based on the implementation of the Generic Dictionary 
    /// </summary>
    /// <remarks> 
    /// <para> 
    /// The generic Dictionary object exposes six interfaces, so this class exposes the
    /// same interfaces. The methods and properties in this file are sorted by which 
    /// interface they come from.
    /// </para>
    /// <para>
    /// The only reason for most of the code in this class is to ensure that the dictionary 
    /// behaves as read-only. All the read methods just delegate to the underlying generic
    /// Dictionary object. All the write methods just throw. 
    /// </para> 
    /// </remarks>
    internal class ReadOnlyDictionary <K, V> : 
            IEnumerable<KeyValuePair<K, V>>,
            ICollection<KeyValuePair<K, V> >,
            IDictionary<K, V>,
            IEnumerable, 
            ICollection,
            IDictionary 
    { 
        //-----------------------------------------------------
        // 
        // Constructors
        //
        //-----------------------------------------------------
 
        #region Constructors
 
        /// <summary> 
        /// Constructor.
        /// </summary> 
        internal
        ReadOnlyDictionary(Dictionary<K,V> dict)
        {
            Invariant.Assert(dict != null); 
            _dict = dict;
        } 
 
        #endregion Constructors
 
        //------------------------------------------------------
        //
        // Public Methods
        // 
        //-----------------------------------------------------
 
        #region Public Methods 

        //------------------------------------------------------ 
        // IEnumerable<KeyValuePair<K, V> > Methods
        //------------------------------------------------------

        #region IEnumerable<KeyValuePair<K, V> > Public Methods 

        /// <summary> 
        /// Returns an enumerator that iterates through the collection. 
        /// </summary>
        IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() 
        {
            return ((IEnumerable<KeyValuePair<K, V>>)_dict).GetEnumerator();
        }
 
        #endregion IEnumerable<KeyValuePair<K, V> > Public Methods
 
        //----------------------------------------------------- 
        // ICollection<KeyValuePair<K, V> > Methods
        //------------------------------------------------------ 

        #region ICollection<KeyValuePair<K, V> > Methods

        /// <summary> 
        /// Adds a new entry to the collection.
        /// </summary> 
        /// <param name="pair"> 
        /// The pair to be added.
        /// </param> 
        /// <exception cref="NotSupportedException">
        /// Always, because the collection is read-only.
        /// </exception>
        public void 
        Add(KeyValuePair<K, V> pair )
        { 
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly)); 
        }
 
        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        /// <exception cref="NotSupportedException"> 
        /// Always, because the collection is read-only.
        /// </exception> 
        public void Clear() 
        {
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly)); 
        }

        /// <summary>
        /// Determines whether the collection contains a specified pair. 
        /// </summary>
        /// <param name="pair"> 
        /// The pair being sought. 
        /// </param>
        public bool Contains(KeyValuePair<K, V> pair ) 
        {
            return ((ICollection<KeyValuePair<K, V> >)_dict).Contains(pair);
        }
 
        /// <summary>
        /// Copies the elements of the collection to an array, starting at the specified 
        /// array index. 
        /// </summary>
        public void 
        CopyTo( KeyValuePair<K, V>[] array, int arrayIndex )
        {
            ((ICollection<KeyValuePair<K, V> >)_dict).CopyTo(array, arrayIndex);
        } 

        /// <summary> 
        /// Removes the first occurrence of the specified pair from the 
        /// collection.
        /// </summary> 
        /// <param name="pair">
        /// The pair to be removed.
        /// </param>
        /// <exception cref="NotSupportedException"> 
        /// Always, because the collection is read-only.
        /// </exception> 
        public bool 
        Remove(
            KeyValuePair<K, V> pair 
            )
        {
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
        } 

        #endregion ICollection<KeyValuePair<K, V> > Methods 
 
        //-----------------------------------------------------
        // IDictionary<K, V> Methods 
        //-----------------------------------------------------

        #region IDictionary<K, V> Methods
 
        /// <summary>
        /// Adds an entry with the specified key (<paramref name="user"/>) and value 
        /// (<paramref name="useLicense"/>) to the dictionary. 
        /// </summary>
        /// <exception cref="NotSupportedException"> 
        /// Always, because the dictionary is read-only.
        /// </exception>
        public void Add(K key, V value)
        { 
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
        } 
 
        /// <summary>
        /// Determines whether the dictionary contains entry fo the specified key. 
        /// </summary>
        /// <returns>
        /// true if the dictionary contains an entry for the specified user, otherwise false.
        /// </returns> 
        public bool ContainsKey(K key)
        { 
            return _dict.ContainsKey(key); 
        }
 
        /// <summary>
        /// Remove the entry with the specified key from the dictionary.
        /// </summary>
        /// <returns> 
        /// true if the element is successfully removed; otherwise, false. This method also returns false
        /// if key was not found in the dictionary. 
        /// </returns> 
        /// <exception cref="NotSupportedException">
        /// Always, because the dictionary is read-only. 
        /// </exception>
        public bool
        Remove(K key)
        { 
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
        } 
 
        /// <summary>
        /// Retrieve the entry associated with the specified key. 
        /// </summary>
        /// <returns>
        /// true if the dictionary contains an entry for the specified key;
        /// otherwise false. 
        /// </returns>
        public bool TryGetValue(K key, out V value) 
        { 
            return _dict.TryGetValue(key, out value);
        } 

        #endregion IDictionary<K,V> Methods

        //----------------------------------------------------- 
        // IEnumerable Methods
        //------------------------------------------------------ 
 
        #region IEnumerable Methods
 
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() 
        {
 
            return ((IEnumerable)_dict).GetEnumerator(); 
        }
 
        #endregion IEnumerable Methods

        //-----------------------------------------------------
        // ICollection Methods 
        //------------------------------------------------------
 
        #region ICollection Methods 

        /// <summary> 
        /// Copies the elements of the collection to an array, starting at the specified
        /// array index.
        /// </summary>
        public void CopyTo(Array array, int index ) 
        {
            ((ICollection)_dict).CopyTo(array, index); 
        } 

        #endregion ICollection Methods 

        //------------------------------------------------------
        // IDictionary Methods
        //----------------------------------------------------- 

        #region IDictionary Methods 
 
        /// <summary>
        /// Adds an element with the specified key and value to the dictionary. 
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always, because the dictionary is read-only.
        /// </exception> 
        public void
        Add(object key, object value) 
        { 
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
        } 

        /// <summary>
        /// Determines whether the dictionary contains an element with the specified key.
        /// </summary> 
        public bool Contains(object key)
        { 
            return ((IDictionary)_dict).Contains(key); 
        }
 
        /// <summary>
        /// Returns an IDictionaryEnumerator for the dictionary.
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator() 
        {
            return ((IDictionary)_dict).GetEnumerator(); 
        } 

        /// <summary> 
        /// Removes the element with the specified key from the dictionary.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always, because the dictionary is read-only. 
        /// </exception>
        public void Remove(object key) 
        { 
            throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
        } 

        #endregion IDictionary Methods

        #endregion Public Methods 

        //------------------------------------------------------ 
        // 
        // Public Properties
        // 
        //-----------------------------------------------------

        #region Public Properties
 
        //-----------------------------------------------------
        // ICollection<KeyValuePair<K, V> > Properties 
        //----------------------------------------------------- 

        #region ICollection<KeyValuePair<K, V> > Properties 

        /// <value>
        /// Returns the number of elements in the collection.
        /// </value> 
        public int Count
        { 
            get { return _dict.Count; } 
        }
 
        /// <value>
        /// Gets a value indicating whether the dictionary is read-only.
        /// </value>
        public bool IsReadOnly 
        {
            get { return true; } 
        } 

        #endregion ICollection<KeyValuePair<K, V> > Properties 

        //------------------------------------------------------
        // IDictionary<K, V> Properties
        //----------------------------------------------------- 

        #region IDictionary<K, V> Properties 
 
        /// <value>
        /// Gets the use license associated with the user specified by <paramref name="user"/>. 
        /// </value>
        public V this[K key]
        {
            get 
            {
                return _dict[key]; 
            } 

            set 
            {
                throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly));
            }
        } 

        /// <value> 
        /// Returns an ICollection containing the keys of the dictionary. 
        /// </value>
        public ICollection<K> Keys 
        {
            get { return ((IDictionary<K, V>)_dict).Keys; }
        }
 
        /// <value>
        /// Returns an ICollection containing the values in the dictionary. 
        /// </value> 
        public ICollection<V> Values
        { 
            get { return ((IDictionary<K, V>)_dict).Values; }
        }

        #endregion IDictionary<K,V> Properties 

        //------------------------------------------------------ 
        // ICollection Properties 
        //------------------------------------------------------
 
        #region ICollection Properties

        public bool IsSynchronized
        { 
            get { return ((ICollection)_dict).IsSynchronized; }
        } 
 
        /// <value>
        /// Gets an object that can be used to synchronize access to the collection. 
        /// </value>
        public object SyncRoot
        {
            get { return ((ICollection)_dict).SyncRoot; } 
        }
 
        #endregion ICollection Properties 

        //----------------------------------------------------- 
        // IDictionary Properties
        //------------------------------------------------------

        #region IDictionary Properties 

        /// <value> 
        /// Gets a value indicating whether the dictionary has a fixed size. 
        /// </value>
        public bool IsFixedSize 
        {
            get { return true; }
        }
 
        /// <value>
        /// Returns an ICollection containing the keys of the dictionary. 
        /// </value> 
        ICollection IDictionary.Keys
        { 
            get { return ((IDictionary)_dict).Keys; }
        }

        /// <value> 
        /// Returns an ICollection containing the values in the dictionary.
        /// </value> 
        ICollection IDictionary.Values 
        {
            get { return ((IDictionary)_dict).Values; } 
        }

        /// <value>
        /// Gets the value associated with the specified key>. 
        /// </value>
        public object this[object key] 
        { 
            get
            { 
                return ((IDictionary)_dict)[key];
            }

            set 
            {
                throw new NotSupportedException(SR.Get(SRID.DictionaryIsReadOnly)); 
            } 
        }
 
        #endregion IDictionary Properties

        #endregion Public Properties
 
        //-----------------------------------------------------
        // 
        // Private Fields 
        //
        //----------------------------------------------------- 
        #region Private Fields

        //
        // The object that provides the implementation of the IDictionary methods. 
        //
        private Dictionary<K, V> _dict; 
        #endregion Private Fields 
    }
} 

