//---------------------------------------------------------------------------- 
//
// <copyright file="ViewManager.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Mapping of (collection, viewName) to CollectionView 
// 
//---------------------------------------------------------------------------
 
/***************************************************************************\
    Avalon data binding supports multiple views over a data collection.
    Each view (CollectionView) is identified by a key (CollectionViewSource), and
    can be sorted and filtered independently of other views. 

    Managing the lifetimes of the views involves some subtle challenges. 
    Do not modify the code in this file until you understand these issues! 

    The fundamental design goal is that a collection should not be responsible 
    for managing its own views.  A collection is a data-centric object, while a
    view (or a set of views) is UI-centric.  Therefore view management is a job
    for UI-related code.  The view manager cannot modify the collection in any
    way, nor can it can assume that the collection has a reference to the view. 

    This principle allows us to define the IDataCollection interface in a 
    system assembly, where it is visible to third-parties who can then 
    implement their own collection classes without having to know anything
    about view management.  It also allows us to create views over collections 
    that don't even implement IDataCollection;  for instance, we support
    views over an IList (and thus over any Array, ArrayList, etc.).

    However, this principle makes lifetime management very tricky.  An 
    application may create views named "A", "B", and "C" over a given
    data collection, and apply a particular sort order to each view.  Next 
    the application may release all its references to view "C", while keeping 
    references to views "A" and "B and to the collection itself.  Then the
    application may refer to view "C" again, and will expect it to still have 
    the same currency, sort order, etc.  Thus view "C" must be kept alive as long
    as the collection itself (or any other view on the collection).  However, once
    the application releases its references to the collection and all its views,
    they should become eligible for garbage collection. 

    If the collection managed its own views, there would be no problem.  You 
    could imagine drawing a dotted line surrounding the collection and its 
    views.  All references related to view management would be contained inside
    this line, so as soon as the application released its references, the 
    objects inside the line could be garbage collected.

    With "external" view management, it gets much harder.  The manager will
    obviously require some references to the collection and its views.  The 
    trick is to create these references in such a way that they keep the objects
    alive as long as necessary, but no longer.  Here's how we do it. 
 
    For each collection, the manager has a ViewTable - a dictionary that maps
    keys into views.  The table contains strong references to the views, and of 
    course each view has a strong reference to its underlying collection.  This
    guarantees that the collection stays alive as long as any view.

    The view manager has a master ViewManager - a dictionary that maps 
    collections to ViewTables.  This is a "global" table, so it must not contain
    a strong reference to a collection (which would keep the collection alive 
    forever).  Instead, it contains weak references to the collection and to 
    its ViewTable.  Now you can draw a dotted line around the collection and
    its ViewTable;  all strong references are either inside the line, or 
    correspond to the application's references to the collection or its views.
    Thus when the application releases all its references, the collection and
    its views can be garbage collected.  This will invalidate the weak references
    in the master ViewManager;  we occasionally purge the table of dead 
    references.
 
    So far, the only reference to the ViewTable is the weak reference stored in 
    the master ViewManager.  This is not enough to keep the ViewTable alive;
    we need some way to keep it alive as long as any of the views in it are alive. 
    We do this by giving each view a strong reference back to its ViewTable,
    using the mysterious ViewManagerData property of a DataCollectionView.  This
    adds more strong references inside the dotted line.  These do not affect
    our garbage collection goal, but do keep the ViewTable alive. 

    The picture:  (arrows with bulbs are weak references:  o---> ) 
 
            ________________________________
           |                |               | 
        /--|-o(collection)  |  (ViewTable)o-|--\
        |  |                |               |   |
        |  |________________________________|   |
        |                                       | 
      _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _
    |   |                                       v              | 
        |                               _______________ 
    |   |                              |               |<--\   |
        |    /-------------------------|---- View "A" -|---| 
    |   v   v                          |               |   |   |
      Collection <---------------------|---- View "B" -|---|
    |       ^                          |               |   |   |
             \-------------------------|---- View "C" -|---| 
    |                                  |_______________|       |
 
    | _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _| 

    not shown:  Strong references from collection to view due to 
    event listeners.


    Dev10 bug 452676 exposed a small flaw in this scheme.  The "not shown" 
    strong reference from the collection to a view (via the CollectionChanged event)
    turns out to be important.  It keeps the view alive at least as 
    long as the collection, even if the app releases all its references to views. 
    If the collection doesn't expose INotifyCollectionChanged, this reference isn't
    present, and you can draw a smaller dotted line around the views and the view 
    table that has no incoming strong references.  This means the view table (and
    the views) can get GC'd.

    We can't fix this.  We need that strong reference, but without an event there's 
    no way to get it (remember, we can't touch the collection itself).  But we can
    mitigate it. 
 
    Here's the mitigation.  The view manager keeps a list of strong references to the
    relevant view tables, each with an "expiration date" (an integer).  At 
    each purge cycle, decrease the expiration dates and discard the ones that reach
    zero.  Whenever there is actual activity on a view table, reset its expiration
    date to the initial value N.  This keeps the view table alive for N purge cycles
    after its last activity, so it can survive a short transition period of inactivity 
    such as the one in bug 452676.  This will also keep the collection alive for up
    to N purge cycles longer than before, which customer may perceive as a leak. 
 
\***************************************************************************/
 
using System;
using System.ComponentModel;

using System.Collections; 
using System.Collections.Specialized;
using System.Windows;         // for exception strings 
using System.Windows.Data; 

namespace MS.Internal.Data 
{

#region WeakRefKey
 
    // for use as the key to a hashtable, when the "real" key is an object
    // that we should not keep alive by a strong reference. 
    internal struct WeakRefKey 
    {
        //----------------------------------------------------- 
        //
        //  Constructors
        //
        //------------------------------------------------------ 

        internal WeakRefKey(object target) 
        { 
            _weakRef = new WeakReference(target);
            _hashCode = (target != null) ? target.GetHashCode() : 314159; 
        }

        //-----------------------------------------------------
        // 
        //  Internal Properties
        // 
        //------------------------------------------------------ 

        internal object Target 
        {
            get { return _weakRef.Target; }
        }
 
        //------------------------------------------------------
        // 
        //  Public Methods 
        //
        //----------------------------------------------------- 

        public override int GetHashCode()
        {
            return _hashCode; 
        }
 
        public override bool Equals(object o) 
        {
            if (o is WeakRefKey) 
            {
                WeakRefKey ck = (WeakRefKey)o;
                object c1 = Target;
                object c2 = ck.Target; 

                if (c1!=null && c2!=null) 
                    return (c1 == c2); 
                else
                    return (_weakRef == ck._weakRef); 
            }
            else
            {
                return false; 
            }
        } 
 
        // overload operator for ==, to be same as Equal implementation.
        public static bool operator ==(WeakRefKey left, WeakRefKey right) 
        {
            if ((object)left == null)
                return (object)right == null;
 
            return left.Equals(right);
        } 
 
        // overload operator for !=, to be same as Equal implementation.
        public static bool operator !=(WeakRefKey left, WeakRefKey right) 
        {
            return !(left == right);
        }
 
        //------------------------------------------------------
        // 
        //  Private Fields 
        //
        //----------------------------------------------------- 

        WeakReference   _weakRef;
        int             _hashCode;  // cache target's hashcode, lest it get GC'd out from under us
    } 

#endregion WeakRefKey 
 
#region ViewTable
 
    internal class ViewTable : HybridDictionary
    {
        //-----------------------------------------------------
        // 
        //  Internal Properties
        // 
        //----------------------------------------------------- 

        internal ViewRecord this[CollectionViewSource cvs] 
        {
            get { return (ViewRecord)base[new WeakRefKey(cvs)]; }
            set { base[new WeakRefKey(cvs)] = value; }
        } 
    }
 
#endregion ViewTable 

#region ViewRecord 

    // A ViewTable holds values of type ViewRecord.  A ViewRecord is a pair
    // [view, version], where view is the collection view and version is the
    // version number in effect when the CollectionViewSource last set the 
    // view's properties.
 
    internal class ViewRecord 
    {
        internal ViewRecord(ICollectionView view) 
        {
            _view = view;
            _version = -1;
        } 

        internal ICollectionView View 
        { 
            get { return _view; }
        } 

        internal int Version
        {
            get { return _version; } 
            set { _version = value; }
        } 
 
        internal bool IsInitialized
        { 
            get { return _isInitialized; }
        }

        internal void InitializeView() 
        {
            _view.MoveCurrentToFirst(); 
            _isInitialized = true; 
        }
 
        ICollectionView     _view;
        int                 _version;
        bool                _isInitialized = false;
    } 

#endregion ViewRecord 
 
#region ViewManager
 
    internal class ViewManager : HybridDictionary
    {
        // This is the N from the mitigation description (see the comment at the
        // top of the file.  Increasing this value enables views to 
        // survive a longer period of inactivity, but also means
        // the collection will live past its normal lifetime a longer time. 
        // There's a tradeoff between robustness and perceived leaking. 
        const int InactivityThreshold = 2;
 
        //------------------------------------------------------
        //
        //  Public Properties
        // 
        //-----------------------------------------------------
 
        public new ViewTable this[object o] 
        {
            get 
            {
                // look up the entry for o
                WeakRefKey key = new WeakRefKey(o);
                WeakReference wr = (WeakReference)base[key]; 

                if (wr != null) 
                { 
                    // we have an entry for o, get its ViewTable
                    ViewTable vt = (ViewTable)wr.Target; 

                    // if the ViewTable has been GC'd, remove the entry
                    if (vt == null)
                        Remove(key); 

                    return vt; 
                } 
                else
                { 
                    // no entry for o
                    return null;
                }
            } 
        }
 
        //------------------------------------------------------ 
        //
        //  Internal Methods 
        //
        //------------------------------------------------------

        internal void Add(object collection, ViewTable vt) 
        {
            base.Add(new WeakRefKey(collection), new WeakReference(vt)); 
        } 

        /// <summary> 
        /// Return the object associated with (collection, cvs, type).
        /// If this is the first reference to this view, add it to the tables.
        /// </summary>
        /// <exception cref="ArgumentException"> 
        /// Thrown when the collectionViewType does not implement ICollectionView
        /// or does not have a constructor that accepts the type of collection. 
        /// Also thrown when the named collection view already exists and is 
        /// not the specified collectionViewType.
        /// </exception> 
        internal ViewRecord GetViewRecord(object collection, CollectionViewSource cvs, Type collectionViewType, bool createView)
        {
            // Order of precendence in acquiring the View:
            // 0) If  collection is already a CollectionView, return it. 
            // 1) If the CollectionView for this collection has been cached, then
            //    return the cached instance. 
            // 2) If a CollectionView derived type has been passed in collectionViewType 
            //    create an instance of that Type
            // 3) If the collection is an ICollectionViewFactory use ICVF.CreateView() 
            //    from the collection
            // 4) If the collection is an IListSource call GetList() and perform 5),
            //    etc. on the returned list
            // 5) If the collection is an IBindingList return a new BindingListCollectionView 
            // 6) If the collection is an IList return a new ListCollectionView
            // 7) If the collection is an IEnumerable, return a new CollectionView 
            //    (it uses the ListEnumerable wrapper) 
            // 8) return null
            // An IListSource must share the view with its underlying list. 

            // if the view already exists, just return it
            // Also, return null if it doesn't exist and we're called in "lazy" mode
            ViewRecord viewRecord = GetExistingView(collection, cvs, collectionViewType); 
            if (viewRecord != null || !createView)
            { 
                return viewRecord; 
            }
 
            // If the collection is an IListSource, it uses the same view as its
            // underlying list.
            IListSource ils = collection as IListSource;
            IList ilsList = null; 
            if (ils != null)
            { 
                ilsList = ils.GetList(); 
                viewRecord = GetExistingView(ilsList, cvs, collectionViewType);
 
                if (viewRecord != null)
                {
                    return CacheView(collection, cvs, (CollectionView)viewRecord.View, viewRecord);
                } 
            }
 
            // Create a new view 
            ICollectionView icv = collection as ICollectionView;
 
            if (icv != null)
            {
                icv = new CollectionViewProxy(icv);
            } 
            else if (collectionViewType == null)
            { 
                // Caller didn't specify a type for the view. 
                ICollectionViewFactory icvf = collection as ICollectionViewFactory;
                if (icvf != null) 
                {
                    // collection is a view factory - call its factory method
                    icv = icvf.CreateView();
                } 
                else
                { 
                    // collection is not a factory - create an appropriate view 
                    IList il = (ilsList != null) ? ilsList : collection as IList;
                    if (il != null) 
                    {
                        // create a view on an IList or IBindingList
                        IBindingList ibl = il as IBindingList;
                        if (ibl != null) 
                            icv = new BindingListCollectionView(ibl);
                        else 
                            icv = new ListCollectionView(il); 
                    }
                    else 
                    {
                        // collection is not IList, wrap it
                        IEnumerable ie = collection as IEnumerable;
                        if (ie != null) 
                        {
                            icv = new EnumerableCollectionView(ie); 
                        } 
                    }
                } 
            }
            else
            {
                // caller specified a type for the view.  Try to honor it. 
                if (!typeof(ICollectionView).IsAssignableFrom(collectionViewType))
                    throw new ArgumentException(SR.Get(SRID.CollectionView_WrongType, collectionViewType.Name)); 
 
                // if collection is IListSource, get its list first (bug 1023903)
                object arg = (ilsList != null) ? ilsList : collection; 

                try
                {
                    icv = Activator.CreateInstance(collectionViewType, 
                                    System.Reflection.BindingFlags.CreateInstance, null,
                                    new object[1]{arg}, null) as ICollectionView; 
                } 
                catch (MissingMethodException e)
                { 
                    throw new ArgumentException(SR.Get(SRID.CollectionView_ViewTypeInsufficient,
                                    collectionViewType.Name, collection.GetType()), e);
                }
            } 

            // if we got a view, add it to the tables 
            if (icv != null) 
            {
                // if the view doesn't derive from CollectionView, create a proxy that does 
                CollectionView cv = icv as CollectionView;
                if (cv == null)
                    cv = new CollectionViewProxy(icv);
 
                if (ilsList != null)    // IListSource's list shares the same view
                    viewRecord = CacheView(ilsList, cvs, cv, null); 
 
                viewRecord = CacheView(collection, cvs, cv, viewRecord);
            } 

            return viewRecord;
        }
 
        // return an existing view (or null if there isn't one) over the collection
        private ViewRecord GetExistingView(object collection, CollectionViewSource cvs, Type collectionViewType) 
        { 
            ViewRecord result;
            CollectionView cv = collection as CollectionView; 

            if (cv == null)
            {
                // look up cached entry 
                ViewTable vt = this[collection];
                if (vt != null) 
                { 
                    ViewRecord vr = vt[cvs];
                    if (vr != null) 
                    {
                        cv = (CollectionView)vr.View;
                    }
                    result = vr; 

                    // activity on the VT - reset its expiration date 
                    if (_inactiveViewTables.Contains(vt)) 
                    {
                        _inactiveViewTables[vt] = InactivityThreshold; 
                    }
                }
                else
                { 
                    result = null;
                } 
            } 
            else
            { 
                // the collection is already a view, just use it directly (no tables needed)
                result = new ViewRecord(cv);
            }
 
            if (cv != null)
            { 
                ValidateViewType(cv, collectionViewType); 
            }
 
            return result;
        }

        private ViewRecord CacheView(object collection, CollectionViewSource cvs, CollectionView cv, ViewRecord vr) 
        {
            // create the view table, if necessary 
            ViewTable vt = this[collection]; 
            if (vt == null)
            { 
                vt = new ViewTable();
                Add(collection, vt);

                // if the collection doesn't implement INCC, it won't hold a strong 
                // reference to its views.  To mitigate Dev10 bug 452676, keep a
                // strong reference to the ViewTable alive for at least a few 
                // Purge cycles.  (See comment at the top of the file.) 
                if (!(collection is INotifyCollectionChanged))
                { 
                    _inactiveViewTables.Add(vt, InactivityThreshold);
                }
            }
 
            // keep the view and the view table alive as long as any view
            // (or the collection itself) is alive 
            if (vr == null) 
                vr = new ViewRecord(cv);
            else if (cv == null) 
                cv = (CollectionView)vr.View;
            cv.SetViewManagerData(vt);

            // add the view to the table 
            vt[cvs] = vr;
            return vr; 
        } 

        // purge the table of dead entries 
        internal bool Purge()
        {
            // decrease the expiration dates of ViewTables on the inactive
            // list, and remove the ones that have expired. 
            int n = _inactiveViewTables.Count;
            if (n > 0) 
            { 
                ViewTable[] keys = new ViewTable[n];
                _inactiveViewTables.Keys.CopyTo(keys, 0); 

                for (int i=0; i<n; ++i)
                {
                    ViewTable vt = keys[i]; 
                    int expirationDate = (int)_inactiveViewTables[vt];
                    if (--expirationDate > 0) 
                    { 
                        _inactiveViewTables[vt] = expirationDate;
                    } 
                    else
                    {
                        _inactiveViewTables.Remove(vt);
                    } 
                }
            } 
 
            // purge the table of entries whose collection has been GC'd.
            ArrayList al = new ArrayList(); 

            foreach (DictionaryEntry de in this)
            {
                WeakRefKey key = (WeakRefKey)de.Key; 
                WeakReference wr = (WeakReference)de.Value;
 
                if (key.Target == null || !wr.IsAlive) 
                    al.Add(key);
            } 

            for (int k=0; k<al.Count; ++k)
            {
                this.Remove(al[k]); 
            }
 
            return (al.Count > 0); 
        }
 
        private void ValidateViewType(CollectionView cv, Type collectionViewType)
        {
            if (collectionViewType != null)
            { 
                // If the view contained in the ViewTable is a proxy of another
                // view, then what we really want to compare is the type of that 
                // other view. 
                CollectionViewProxy cvp = cv as CollectionViewProxy;
                Type cachedViewType = (cvp == null) ? cv.GetType() : cvp.ProxiedView.GetType(); 

                if (cachedViewType != collectionViewType)
                    throw new ArgumentException(SR.Get(SRID.CollectionView_NameTypeDuplicity, collectionViewType, cachedViewType));
            } 
        }
 
        HybridDictionary _inactiveViewTables = new HybridDictionary(); 
    }
 
#endregion ViewManager

}
 

