//---------------------------------------------------------------------------- 
//
// <copyright file="Vector3DKeyFrameCollection.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This file was generated, please do not edit it directly. 
// 
// Please see http://wiki/default.aspx/Microsoft.Projects.Avalon/MilCodeGen.html for more information.
// 
//---------------------------------------------------------------------------

using MS.Internal;
 
using System;
using System.Collections; 
using System.Collections.Generic; 
using System.ComponentModel;
using System.Diagnostics; 
using System.Globalization;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
 
namespace System.Windows.Media.Animation
{ 
    /// <summary> 
    /// This collection is used in conjunction with a KeyFrameVector3DAnimation
    /// to animate a Vector3D property value along a set of key frames. 
    /// </summary>
    public class Vector3DKeyFrameCollection : Freezable, IList
    {
        #region Data 

        private List<Vector3DKeyFrame> _keyFrames; 
        private static Vector3DKeyFrameCollection s_emptyCollection; 

        #endregion 

        #region Constructors

        /// <Summary> 
        /// Creates a new Vector3DKeyFrameCollection.
        /// </Summary> 
        public Vector3DKeyFrameCollection() 
            : base()
        { 
            _keyFrames = new List< Vector3DKeyFrame>(2);
        }

        #endregion 

        #region Static Methods 
 
        /// <summary>
        /// An empty Vector3DKeyFrameCollection. 
        /// </summary>
        public static Vector3DKeyFrameCollection Empty
        {
            get 
            {
                if (s_emptyCollection == null) 
                { 
                    Vector3DKeyFrameCollection emptyCollection = new Vector3DKeyFrameCollection();
 
                    emptyCollection._keyFrames = new List< Vector3DKeyFrame>(0);
                    emptyCollection.Freeze();

                    s_emptyCollection = emptyCollection; 
                }
 
                return s_emptyCollection; 
            }
        } 

        #endregion

        #region Freezable 

        /// <summary> 
        /// Creates a freezable copy of this Vector3DKeyFrameCollection. 
        /// </summary>
        /// <returns>The copy</returns> 
        public new Vector3DKeyFrameCollection Clone()
        {
            return (Vector3DKeyFrameCollection)base.Clone();
        } 

        /// <summary> 
        /// Implementation of <see cref="System.Windows.Freezable.CreateInstanceCore">Freezable.CreateInstanceCore</see>. 
        /// </summary>
        /// <returns>The new Freezable.</returns> 
        protected override Freezable CreateInstanceCore()
        {
            return new Vector3DKeyFrameCollection();
        } 

        /// <summary> 
        /// Implementation of <see cref="System.Windows.Freezable.CloneCore(System.Windows.Freezable)">Freezable.CloneCore</see>. 
        /// </summary>
        protected override void CloneCore(Freezable sourceFreezable) 
        {
            Vector3DKeyFrameCollection sourceCollection = (Vector3DKeyFrameCollection) sourceFreezable;
            base.CloneCore(sourceFreezable);
 
            int count = sourceCollection._keyFrames.Count;
 
            _keyFrames = new List< Vector3DKeyFrame>(count); 

            for (int i = 0; i < count; i++) 
            {
                Vector3DKeyFrame keyFrame = (Vector3DKeyFrame)sourceCollection._keyFrames[i].Clone();
                _keyFrames.Add(keyFrame);
                OnFreezablePropertyChanged(null, keyFrame); 
            }
        } 
 

        /// <summary> 
        /// Implementation of <see cref="System.Windows.Freezable.CloneCurrentValueCore(System.Windows.Freezable)">Freezable.CloneCurrentValueCore</see>.
        /// </summary>
        protected override void CloneCurrentValueCore(Freezable sourceFreezable)
        { 
            Vector3DKeyFrameCollection sourceCollection = (Vector3DKeyFrameCollection) sourceFreezable;
            base.CloneCurrentValueCore(sourceFreezable); 
 
            int count = sourceCollection._keyFrames.Count;
 
            _keyFrames = new List< Vector3DKeyFrame>(count);

            for (int i = 0; i < count; i++)
            { 
                Vector3DKeyFrame keyFrame = (Vector3DKeyFrame)sourceCollection._keyFrames[i].CloneCurrentValue();
                _keyFrames.Add(keyFrame); 
                OnFreezablePropertyChanged(null, keyFrame); 
            }
        } 


        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.GetAsFrozenCore(System.Windows.Freezable)">Freezable.GetAsFrozenCore</see>. 
        /// </summary>
        protected override void GetAsFrozenCore(Freezable sourceFreezable) 
        { 
            Vector3DKeyFrameCollection sourceCollection = (Vector3DKeyFrameCollection) sourceFreezable;
            base.GetAsFrozenCore(sourceFreezable); 

            int count = sourceCollection._keyFrames.Count;

            _keyFrames = new List< Vector3DKeyFrame>(count); 

            for (int i = 0; i < count; i++) 
            { 
                Vector3DKeyFrame keyFrame = (Vector3DKeyFrame)sourceCollection._keyFrames[i].GetAsFrozen();
                _keyFrames.Add(keyFrame); 
                OnFreezablePropertyChanged(null, keyFrame);
            }
        }
 

        /// <summary> 
        /// Implementation of <see cref="System.Windows.Freezable.GetCurrentValueAsFrozenCore(System.Windows.Freezable)">Freezable.GetCurrentValueAsFrozenCore</see>. 
        /// </summary>
        protected override void GetCurrentValueAsFrozenCore(Freezable sourceFreezable) 
        {
            Vector3DKeyFrameCollection sourceCollection = (Vector3DKeyFrameCollection) sourceFreezable;
            base.GetCurrentValueAsFrozenCore(sourceFreezable);
 
            int count = sourceCollection._keyFrames.Count;
 
            _keyFrames = new List< Vector3DKeyFrame>(count); 

            for (int i = 0; i < count; i++) 
            {
                Vector3DKeyFrame keyFrame = (Vector3DKeyFrame)sourceCollection._keyFrames[i].GetCurrentValueAsFrozen();
                _keyFrames.Add(keyFrame);
                OnFreezablePropertyChanged(null, keyFrame); 
            }
        } 
 
        /// <summary>
        /// 
        /// </summary>
        protected override bool FreezeCore(bool isChecking)
        {
            bool canFreeze = base.FreezeCore(isChecking); 

            for (int i = 0; i < _keyFrames.Count && canFreeze; i++) 
            { 
                canFreeze &= Freezable.Freeze(_keyFrames[i], isChecking);
            } 

            return canFreeze;
        }
 
        #endregion
 
        #region IEnumerable 

        /// <summary> 
        /// Returns an enumerator of the Vector3DKeyFrames in the collection.
        /// </summary>
        public IEnumerator GetEnumerator()
        { 
            ReadPreamble();
 
            return _keyFrames.GetEnumerator(); 
        }
 
        #endregion

        #region ICollection
 
        /// <summary>
        /// Returns the number of Vector3DKeyFrames in the collection. 
        /// </summary> 
        public int Count
        { 
            get
            {
                ReadPreamble();
 
                return _keyFrames.Count;
            } 
        } 

        /// <summary> 
        /// See <see cref="System.Collections.ICollection.IsSynchronized">ICollection.IsSynchronized</see>.
        /// </summary>
        public bool IsSynchronized
        { 
            get
            { 
                ReadPreamble(); 

                return (IsFrozen || Dispatcher != null); 
            }
        }

        /// <summary> 
        /// See <see cref="System.Collections.ICollection.SyncRoot">ICollection.SyncRoot</see>.
        /// </summary> 
        public object SyncRoot 
        {
            get 
            {
                ReadPreamble();

                return ((ICollection)_keyFrames).SyncRoot; 
            }
        } 
 
        /// <summary>
        /// Copies all of the Vector3DKeyFrames in the collection to an 
        /// array.
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        { 
            ReadPreamble();
 
            ((ICollection)_keyFrames).CopyTo(array, index); 
        }
 
        /// <summary>
        /// Copies all of the Vector3DKeyFrames in the collection to an
        /// array of Vector3DKeyFrames.
        /// </summary> 
        public void CopyTo(Vector3DKeyFrame[] array, int index)
        { 
            ReadPreamble(); 

            _keyFrames.CopyTo(array, index); 
        }

        #endregion
 
        #region IList
 
        /// <summary> 
        /// Adds a Vector3DKeyFrame to the collection.
        /// </summary> 
        int IList.Add(object keyFrame)
        {
            return Add((Vector3DKeyFrame)keyFrame);
        } 

        /// <summary> 
        /// Adds a Vector3DKeyFrame to the collection. 
        /// </summary>
        public int Add(Vector3DKeyFrame keyFrame) 
        {
            if (keyFrame == null)
            {
                throw new ArgumentNullException("keyFrame"); 
            }
 
            WritePreamble(); 

            OnFreezablePropertyChanged(null, keyFrame); 
            _keyFrames.Add(keyFrame);

            WritePostscript();
 
            return _keyFrames.Count - 1;
        } 
 
        /// <summary>
        /// Removes all Vector3DKeyFrames from the collection. 
        /// </summary>
        public void Clear()
        {
            WritePreamble(); 

            if (_keyFrames.Count > 0) 
            { 
                for (int i = 0; i < _keyFrames.Count; i++)
                { 
                    OnFreezablePropertyChanged(_keyFrames[i], null);
                }

                _keyFrames.Clear(); 

                WritePostscript(); 
            } 
        }
 
        /// <summary>
        /// Returns true of the collection contains the given Vector3DKeyFrame.
        /// </summary>
        bool IList.Contains(object keyFrame) 
        {
            return Contains((Vector3DKeyFrame)keyFrame); 
        } 

        /// <summary> 
        /// Returns true of the collection contains the given Vector3DKeyFrame.
        /// </summary>
        public bool Contains(Vector3DKeyFrame keyFrame)
        { 
            ReadPreamble();
 
            return _keyFrames.Contains(keyFrame); 
        }
 
        /// <summary>
        /// Returns the index of a given Vector3DKeyFrame in the collection.
        /// </summary>
        int IList.IndexOf(object keyFrame) 
        {
            return IndexOf((Vector3DKeyFrame)keyFrame); 
        } 

        /// <summary> 
        /// Returns the index of a given Vector3DKeyFrame in the collection.
        /// </summary>
        public int IndexOf(Vector3DKeyFrame keyFrame)
        { 
            ReadPreamble();
 
            return _keyFrames.IndexOf(keyFrame); 
        }
 
        /// <summary>
        /// Inserts a Vector3DKeyFrame into a specific location in the collection.
        /// </summary>
        void IList.Insert(int index, object keyFrame) 
        {
            Insert(index, (Vector3DKeyFrame)keyFrame); 
        } 

        /// <summary> 
        /// Inserts a Vector3DKeyFrame into a specific location in the collection.
        /// </summary>
        public void Insert(int index, Vector3DKeyFrame keyFrame)
        { 
            if (keyFrame == null)
            { 
                throw new ArgumentNullException("keyFrame"); 
            }
 
            WritePreamble();

            OnFreezablePropertyChanged(null, keyFrame);
            _keyFrames.Insert(index, keyFrame); 

            WritePostscript(); 
        } 

        /// <summary> 
        /// Returns true if the collection is frozen.
        /// </summary>
        public bool IsFixedSize
        { 
            get
            { 
                ReadPreamble(); 

                return IsFrozen; 
            }
        }

        /// <summary> 
        /// Returns true if the collection is frozen.
        /// </summary> 
        public bool IsReadOnly 
        {
            get 
            {
                ReadPreamble();

                return IsFrozen; 
            }
        } 
 
        /// <summary>
        /// Removes a Vector3DKeyFrame from the collection. 
        /// </summary>
        void IList.Remove(object keyFrame)
        {
            Remove((Vector3DKeyFrame)keyFrame); 
        }
 
        /// <summary> 
        /// Removes a Vector3DKeyFrame from the collection.
        /// </summary> 
        public void Remove(Vector3DKeyFrame keyFrame)
        {
            WritePreamble();
 
            if (_keyFrames.Contains(keyFrame))
            { 
                OnFreezablePropertyChanged(keyFrame, null); 
                _keyFrames.Remove(keyFrame);
 
                WritePostscript();
            }
        }
 
        /// <summary>
        /// Removes the Vector3DKeyFrame at the specified index from the collection. 
        /// </summary> 
        public void RemoveAt(int index)
        { 
            WritePreamble();

            OnFreezablePropertyChanged(_keyFrames[index], null);
            _keyFrames.RemoveAt(index); 

            WritePostscript(); 
        } 

        /// <summary> 
        /// Gets or sets the Vector3DKeyFrame at a given index.
        /// </summary>
        object IList.this[int index]
        { 
            get
            { 
                return this[index]; 
            }
            set 
            {
                this[index] = (Vector3DKeyFrame)value;
            }
        } 

        /// <summary> 
        /// Gets or sets the Vector3DKeyFrame at a given index. 
        /// </summary>
        public Vector3DKeyFrame this[int index] 
        {
            get
            {
                ReadPreamble(); 

                return _keyFrames[index]; 
            } 
            set
            { 
                if (value == null)
                {
                    throw new ArgumentNullException(String.Format(CultureInfo.InvariantCulture, "Vector3DKeyFrameCollection[{0}]", index));
                } 

                WritePreamble(); 
 
                if (value != _keyFrames[index])
                { 
                    OnFreezablePropertyChanged(_keyFrames[index], value);
                    _keyFrames[index] = value;

                    Debug.Assert(_keyFrames[index] != null); 

                    WritePostscript(); 
                } 
            }
        } 

        #endregion
    }
} 

